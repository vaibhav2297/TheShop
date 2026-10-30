namespace TheShop.Application.Common.Models;

/// <summary>Uploaded image bytes and metadata. Each feature validates its own image requirements.</summary>
public sealed record ImageUpload(byte[] Content, string FileName, string ContentType);
