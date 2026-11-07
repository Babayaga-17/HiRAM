namespace ASI.Basecode.Services.ServiceModels;

public class SupabaseStorageOptions
{
    public const string SectionName = "SupabaseStorage";
    public string ProjectUrl { get; set; } = string.Empty;
    public string SecretKey { get; set; } = string.Empty;
    public string Bucket { get; set; } = "equipment-images";
    public string ProfileBucket { get; set; } = "profile-images";
}
