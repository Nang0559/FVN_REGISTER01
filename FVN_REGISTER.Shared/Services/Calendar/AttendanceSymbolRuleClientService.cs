using FVN_REGISTER.Contract.Dtos.Calendar;
using FVN_REGISTER.Contract.Responses;
using FVN_REGISTER.Shared.Handlers;

namespace FVN_REGISTER.Shared.Services.Calendar;

public sealed class AttendanceSymbolRuleClientService
{
    private readonly IHttpClientWithAuth _http;
    public AttendanceSymbolRuleClientService(IHttpClientWithAuth http)=>_http=http;
    public Task<ApiResponse<List<AttendanceSymbolRuleDto>>> GetAllAsync(CancellationToken ct=default)=>_http.GetAsync<List<AttendanceSymbolRuleDto>>("api/attendance-symbol-rules",ct);
    public Task<ApiResponse<AttendanceSymbolRuleDto>> CreateAsync(AttendanceSymbolRuleUpsertRequest request,CancellationToken ct=default)=>_http.PostAsync<AttendanceSymbolRuleDto>("api/attendance-symbol-rules",request,ct);
    public Task<ApiResponse<AttendanceSymbolRuleDto>> UpdateAsync(int id,AttendanceSymbolRuleUpsertRequest request,CancellationToken ct=default)=>_http.PutAsync<AttendanceSymbolRuleDto>($"api/attendance-symbol-rules/{id}",request,ct);
    public Task<ApiResponse<object>> DeleteAsync(int id,CancellationToken ct=default)=>_http.DeleteAsync<object>($"api/attendance-symbol-rules/{id}",ct);
    public Task<ApiResponse<AttendanceSymbolRuleTestResultDto>> TestAsync(AttendanceSymbolRuleTestRequest request,CancellationToken ct=default)=>_http.PostAsync<AttendanceSymbolRuleTestResultDto>("api/attendance-symbol-rules/test",request,ct);
}
