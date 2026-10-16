namespace FVN_REGISTER.Contract.Dtos.SystemUpdate
{
    public sealed class PatchStepDto
    {
        public DateTime? At { get; set; }
        public string Text { get; set; } = string.Empty;
    }

    public sealed class PatchStatusDto
    {
        public string Id { get; set; } = string.Empty;
        public string State { get; set; } = string.Empty;
        public string? Version { get; set; }
        public string? Message { get; set; }
        public string? UploadedBy { get; set; }
        public DateTime? StartedAt { get; set; }
        public DateTime? FinishedAt { get; set; }
        public List<PatchStepDto> Steps { get; set; } = new();
    }

    public sealed class SystemUpdateOverviewDto
    {
        public bool Enabled { get; set; }
        public long MaxPackageBytes { get; set; }
        public PatchStatusDto? Current { get; set; }
        public List<string> PendingPackages { get; set; } = new();
        public List<PatchStatusDto> History { get; set; } = new();
    }

    public sealed class PatchUploadResultDto
    {
        public string FileName { get; set; } = string.Empty;
        public string Version { get; set; } = string.Empty;
    }
}