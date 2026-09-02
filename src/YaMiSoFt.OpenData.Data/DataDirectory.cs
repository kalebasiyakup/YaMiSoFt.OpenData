namespace YaMiSoFt.OpenData.Data;

/// <summary>
/// The resolved directory the datasets were loaded from.
/// </summary>
/// <remarks>
/// A named type rather than a bare string so it can be registered and injected without
/// colliding with every other string in the container, and so the resolution happens once
/// instead of in each store's factory.
/// </remarks>
/// <param name="Path">Absolute path to the dataset directory.</param>
public sealed record DataDirectory(string Path);
