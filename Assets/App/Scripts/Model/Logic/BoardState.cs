using System;

public class BoardState
{
    public const int MAX_SIZE = 12;
    public CellData[] Cells;

    // 現在の盤面の有効範囲（正方形を前提しているのでSize1つでも良いが、柔軟性のためWidth/Heightを残す）
    public int Width { get; private set; }
    public int Height { get; private set; }

    // 仮想座標 (0,0) が、実配列 Cells のどこに対応するかを示すオフセット
    private int _originX;
    private int _originY;

    public BoardState(int initialSize = 8)
    {
        // ヒープアロケーションは最初の一回のみ
        Cells = new CellData[MAX_SIZE * MAX_SIZE];

        // 初期サイズ設定
        Width = initialSize;
        Height = initialSize;

        // 真ん中に配置されるようにオフセットを計算
        // 例: 12x12 の中で 8x8 を使うなら、開始位置は (2, 2)
        _originX = (MAX_SIZE - initialSize) / 2;
        _originY = (MAX_SIZE - initialSize) / 2;

        InitializeBoard();
    }

    // コピーコンストラクタ（AI用）
    public BoardState(BoardState source)
    {
        Cells = new CellData[MAX_SIZE * MAX_SIZE];
        Array.Copy(source.Cells, Cells, source.Cells.Length);

        Width = source.Width;
        Height = source.Height;
        _originX = source._originX;
        _originY = source._originY;
    }

    private void InitializeBoard()
    {
        // 仮想座標系における中心
        int cx = Width / 2;
        int cy = Height / 2;
        SetCell(cx - 1, cy - 1, StoneColor.Black, StoneType.Normal);
        SetCell(cx, cy - 1, StoneColor.White, StoneType.Normal);
        SetCell(cx - 1, cy, StoneColor.White, StoneType.Normal);
        SetCell(cx, cy, StoneColor.Black, StoneType.Normal);
    }

    /// <summary>
    /// 仮想座標 (x, y) を実配列のインデックスに変換してアクセスする
    /// 外部（RulesやAI）は、常に (0,0) を左上として扱える
    /// </summary>
    public CellData GetCell(int x, int y)
    {
        if (x < 0 || x >= Width || y < 0 || y >= Height)
            return new CellData { Color = StoneColor.Wall };

        // オフセットを加算して実配列にアクセス
        int realX = _originX + x;
        int realY = _originY + y;

        // 実配列の範囲外チェック
        if (realX < 0 || realX >= MAX_SIZE || realY < 0 || realY >= MAX_SIZE)
            throw new IndexOutOfRangeException("BoardState.GetCell: _originX または _originY が不正値の可能性があります");

        return Cells[realY * MAX_SIZE + realX];
    }

    public void SetCell(int x, int y, StoneColor c, StoneType t)
    {
        if (x < 0 || x >= Width || y < 0 || y >= Height)
            return;

        int realX = _originX + x;
        int realY = _originY + y;

        if (realX < 0 || realX >= MAX_SIZE || realY < 0 || realY >= MAX_SIZE)
            throw new IndexOutOfRangeException("BoardState.SetCell: _originX または _originY が不正値の可能性があります");

        int idx = realY * MAX_SIZE + realX;
        Cells[idx].Color = c;
        Cells[idx].Type = t;
        Cells[idx].IsFixed = (t == StoneType.Fixed);
    }

    /// <summary>
    /// 盤面拡張 (Zero-Copy Implementation)
    /// 配列の中身は一切動かさず、有効範囲（窓）を広げるだけ。O(1)。
    /// </summary>
    public void ExpandBoard()
    {
        // これ以上広げられない（物理配列の限界）
        if (_originX - 1 < 0 || _originY - 1 < 0 || _originX + Width + 1 > MAX_SIZE || _originY + Height + 1 > MAX_SIZE)
            return;

        // オフセット（原点）を左上にずらす
        _originX--;
        _originY--;

        // サイズを広げる
        Width += 2;
        Height += 2;
    }
}
