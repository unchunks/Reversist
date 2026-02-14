// ---------------------------------------------------------
// DATA: Game Configuration
// タイトル画面からゲーム画面へ設定を渡すためのコンテナ
// ---------------------------------------------------------

public static class GameSettings
{
    public enum GameMode
    {
        PvP,
        PvE
    }

    public enum PlayerSide
    {
        Black,  // 先攻
        White,  // 後攻
        Random  // ランダム
    }

    public static GameMode Mode = GameMode.PvP;
    public static PlayerSide Side = PlayerSide.Black;
    public static int AiDifficulty = 1; // 探索の深さ

    // 設定をリセットする場合
    public static void Reset()
    {
        Mode = GameMode.PvP;
        AiDifficulty = 1;
    }
}
