using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Sheet_Music_App.Models;

namespace Sheet_Music_App.Storage
{
    public class LocalFolderStorage : IProjectStorage
    {
        private string _rootPath;
        private const string ProjectsFolderName = "Projects";
        private const string ProjectFileName = "project.json";

        public LocalFolderStorage(string? rootPath = null)
        {
            _rootPath = rootPath ?? GetDefaultDocumentsPath();
            EnsureRoot();
        }

        // Return the expected project folder path for a given project model
        public string GetProjectFolderPath(Project project)
        {
            var projectFolder = Path.Combine(_rootPath, ProjectsFolderName, MakeSafeProjectFolderName(project));
            return projectFolder;
        }

        public Task SetRootPathAsync(string rootPath, CancellationToken ct = default)
        {
            _rootPath = rootPath ?? throw new ArgumentNullException(nameof(rootPath));
            EnsureRoot();
            return Task.CompletedTask;
        }

        public async Task<Project> CreateProjectAsync(Project model, CancellationToken ct = default)
        {
            if (model == null) throw new ArgumentNullException(nameof(model));

            string projectFolder = Path.Combine(_rootPath, ProjectsFolderName, MakeSafeProjectFolderName(model));
            Directory.CreateDirectory(projectFolder);

            string projectFile = Path.Combine(projectFolder, ProjectFileName);
            model.Created = DateTime.UtcNow;
            model.LastModified = DateTime.UtcNow;

            await WriteJsonAtomicAsync(projectFile, model, ct).ConfigureAwait(false);
            return model;
        }

        public async Task DeleteProjectAsync(Guid projectId, CancellationToken ct = default)
        {
            var dir = FindProjectDirectory(projectId);
            if (dir == null) return;
            Directory.Delete(dir, true);
            await Task.CompletedTask;
        }

        public async Task<Project?> LoadProjectAsync(Guid projectId, CancellationToken ct = default)
        {
            var dir = FindProjectDirectory(projectId);
            if (dir == null) return null;

            var projectFile = Path.Combine(dir, ProjectFileName);
            if (!File.Exists(projectFile)) return null;

            using var fs = File.OpenRead(projectFile);
            var project = await JsonSerializer.DeserializeAsync<Project>(fs, cancellationToken: ct).ConfigureAwait(false);
            return project;
        }

        public async Task<IEnumerable<ProjectSummary>> ListProjectsAsync(CancellationToken ct = default)
        {
            var projectsRoot = Path.Combine(_rootPath, ProjectsFolderName);
            if (!Directory.Exists(projectsRoot)) return Enumerable.Empty<ProjectSummary>();

            var dirs = Directory.GetDirectories(projectsRoot);
            var result = new List<ProjectSummary>();

            foreach (var dir in dirs)
            {
                var projectFile = Path.Combine(dir, ProjectFileName);
                if (!File.Exists(projectFile)) continue;

                try
                {
                    using var fs = File.OpenRead(projectFile);
                    var proj = await JsonSerializer.DeserializeAsync<Project>(fs, cancellationToken: ct).ConfigureAwait(false);
                    if (proj != null)
                    {
                        result.Add(new ProjectSummary
                        {
                            Id = proj.Id,
                            Name = proj.Name,
                            LastModified = proj.LastModified,
                            Provider = proj.Storage?.Provider ?? "Local",
                            Location = dir
                        });
                    }
                }
                catch
                {
                    // ignore malformed project files
                }
            }

            return result;
        }

        public StorageProviderInfo GetProviderInfo()
        {
            return new StorageProviderInfo { ProviderName = "LocalFolder", RootPath = _rootPath };
        }

        public async Task SaveProjectAsync(Project model, CancellationToken ct = default)
        {
            if (model == null) throw new ArgumentNullException(nameof(model));
            var dir = FindProjectDirectory(model.Id);
            if (dir == null)
            {
                // create new
                await CreateProjectAsync(model, ct).ConfigureAwait(false);
                return;
            }

            var projectFile = Path.Combine(dir, ProjectFileName);
            model.LastModified = DateTime.UtcNow;
            await WriteJsonAtomicAsync(projectFile, model, ct).ConfigureAwait(false);
        }

        private void EnsureRoot()
        {
            var projectsRoot = Path.Combine(_rootPath, ProjectsFolderName);
            Directory.CreateDirectory(projectsRoot);
        }

        private static string GetDefaultDocumentsPath()
        {
            var docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            return Path.Combine(docs, "Sheet Music App");
        }

        private static string MakeSafeProjectFolderName(Project project)
        {
            var safe = string.IsNullOrWhiteSpace(project.Name) ? project.Id.ToString() : project.Name;
            return MakeSafeFileName(project.Id.ToString() + "-" + safe);
        }

        private static string MakeSafeFileName(string name)
        {
            foreach (var c in Path.GetInvalidFileNameChars())
            {
                name = name.Replace(c, '_');
            }

            return name;
        }

        private string? FindProjectDirectory(Guid projectId)
        {
            var projectsRoot = Path.Combine(_rootPath, ProjectsFolderName);
            if (!Directory.Exists(projectsRoot)) return null;
            var dirs = Directory.GetDirectories(projectsRoot);
            foreach (var dir in dirs)
            {
                var projectFile = Path.Combine(dir, ProjectFileName);
                if (!File.Exists(projectFile)) continue;
                try
                {
                    using var fs = File.OpenRead(projectFile);
                    var proj = JsonSerializer.Deserialize<Project>(fs);
                    if (proj != null && proj.Id == projectId) return dir;
                }
                catch
                {
                    // ignore
                }
            }

            return null;
        }

        private static async Task WriteJsonAtomicAsync<T>(string filePath, T value, CancellationToken ct = default)
        {
            var temp = Path.Combine(Path.GetDirectoryName(filePath) ?? Path.GetTempPath(), Path.GetFileName(filePath) + ".tmp");
            var options = new JsonSerializerOptions { WriteIndented = true };
            using (var fs = File.Create(temp))
            {
                await JsonSerializer.SerializeAsync(fs, value, options, ct).ConfigureAwait(false);
            }

            // Replace existing file atomically
            File.Move(temp, filePath, true);
        }
    }
}
