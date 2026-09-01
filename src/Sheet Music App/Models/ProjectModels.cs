using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Sheet_Music_App.Models
{
    public class Project
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public DateTime Created { get; set; } = DateTime.UtcNow;
        public DateTime LastModified { get; set; } = DateTime.UtcNow;
        public List<Piece> Pieces { get; set; } = new List<Piece>();
        public StorageInfo Storage { get; set; } = new StorageInfo();
        public int Version { get; set; } = 1;
    }

    public class Piece
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Title { get; set; } = string.Empty;
        public string Composer { get; set; } = string.Empty;
        public List<PdfDocument> Pdfs { get; set; } = new List<PdfDocument>();
    }

    public class PdfDocument
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string FileName { get; set; } = string.Empty; // stored file name in project folder
        public int PageCount { get; set; }
    }

    public class StorageInfo
    {
        public string Provider { get; set; } = "Local"; // "Local" or "OneDrive"
        public string Path { get; set; } = string.Empty; // user-chosen root path for project storage
    }

    public class ProjectSummary
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public DateTime LastModified { get; set; }
        public string Provider { get; set; } = string.Empty;
        public string Location { get; set; } = string.Empty;
    }

    public class AnnotationMetadata
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public int PageNumber { get; set; }
        public DateTime Created { get; set; } = DateTime.UtcNow;
        public DateTime Modified { get; set; } = DateTime.UtcNow;
        public string Author { get; set; } = string.Empty;
    }
}
