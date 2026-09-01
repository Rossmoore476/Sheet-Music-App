using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Sheet_Music_App.Models;

namespace Sheet_Music_App.Storage
{
    public interface IProjectStorage
    {
        Task SetRootPathAsync(string rootPath, CancellationToken ct = default);
        Task<Project> CreateProjectAsync(Project model, CancellationToken ct = default);
        Task SaveProjectAsync(Project model, CancellationToken ct = default);
        Task<Project?> LoadProjectAsync(Guid projectId, CancellationToken ct = default);
        Task<IEnumerable<ProjectSummary>> ListProjectsAsync(CancellationToken ct = default);
        Task DeleteProjectAsync(Guid projectId, CancellationToken ct = default);
        StorageProviderInfo GetProviderInfo();
    }

    public class StorageProviderInfo
    {
        public string ProviderName { get; set; } = string.Empty;
        public string RootPath { get; set; } = string.Empty;
    }
}
