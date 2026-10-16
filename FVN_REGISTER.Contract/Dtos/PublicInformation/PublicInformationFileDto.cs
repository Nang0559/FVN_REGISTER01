namespace FVN_REGISTER.Contract.Dtos.PublicInformation;

public sealed class PublicInformationFileDto
{
    public byte[] Content { get; set; } = Array.Empty<byte>();
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = "application/octet-stream";
}
