using System.Net.Http.Headers;
using FVN_REGISTER.Contract.Dtos.SystemUpdate;
using FVN_REGISTER.Contract.Responses;
using FVN_REGISTER.Shared.Handlers;

namespace FVN_REGISTER.Shared.Services.SystemUpdate
{
    public interface ISystemUpdateClientService
    {
        Task<ApiResponse<SystemUpdateOverviewDto>> GetOverviewAsync(CancellationToken ct = default);
        Task<ApiResponse<PatchUploadResultDto>> UploadAsync(Stream package, string fileName, CancellationToken ct = default);
    }

    public sealed class SystemUpdateClientService : ISystemUpdateClientService
    {
        private readonly IHttpClientWithAuth _http;
        public SystemUpdateClientService(IHttpClientWithAuth http) => _http = http;

        public Task<ApiResponse<SystemUpdateOverviewDto>> GetOverviewAsync(CancellationToken ct = default)
            => _http.GetAsync<SystemUpdateOverviewDto>("api/systemupdate/overview", ct);

        public Task<ApiResponse<PatchUploadResultDto>> UploadAsync(Stream package, string fileName, CancellationToken ct = default)
        {
            var content = new MultipartFormDataContent();
            var file = new StreamContent(package);
            file.Headers.ContentType = new MediaTypeHeaderValue("application/zip");
            content.Add(file, "file", fileName);
            return _http.PostMultipartAsync<PatchUploadResultDto>("api/systemupdate/upload", content, ct);
        }
    }
}