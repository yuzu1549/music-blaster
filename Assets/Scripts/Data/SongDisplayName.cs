/// <summary>
/// 内部の曲名を、画面に表示する曲名へ変換する。
/// </summary>
public static class SongDisplayName
{
    public static string Get(string songId)
    {
        return songId == "SnowDrive0123"
            ? "SnowDrive(01.23)"
            : songId;
    }
}
