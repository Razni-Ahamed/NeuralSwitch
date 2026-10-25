using System.Diagnostics;

namespace NeuralSwitch.Core.Model;

/// <summary>A copy of NVIDIA's DLSS 5 Neural Rendering model, nvngx_dlssnr.dll, and whether NVIDIA signed it.</summary>
/// <param name="Signer">Who signed it, when the signature is valid. Null for an unsigned or tampered file.</param>
/// <param name="SignerOrganization">The organisation on the signing certificate.</param>
public sealed record ModelInfo(string Path, string? Version, long Size, string? Signer, string? SignerOrganization)
{
    public const string FileName = "nvngx_dlssnr.dll";

    /// <summary>Carries a valid signature from NVIDIA. Modified copies of this file circulate; those do not.</summary>
    public bool IsSignedByNvidia => SignerOrganization == "NVIDIA Corporation";

    public static ModelInfo? Inspect(string path)
    {
        if (!File.Exists(path))
            return null;

        var info = FileVersionInfo.GetVersionInfo(path);
        var version = $"{info.FileMajorPart}.{info.FileMinorPart}.{info.FileBuildPart}.{info.FilePrivatePart}";
        var signer = Authenticode.VerifiedSigner(path);
        return new ModelInfo(path, version, new FileInfo(path).Length, signer?.Name, signer?.Organization);
    }
}
