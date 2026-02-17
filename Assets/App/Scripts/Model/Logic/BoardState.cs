using System;

public class BoardState
{
    public const int MAX_SIZE = 12;
    public CellData[] Cells;

    // 現在の盤面の有効範囲（正方形を前提しているのでSize1つでも良いが、柔軟性のためWidth/Heightを残しておく）
    public int Width { get; private set; }
    public int Height { get; private set; }

    // 仮想座標 (0,0) が、実配列 Cells のどこに対応するかを示すオフセット
    private int _originX;
    private int _originY;
    public int OriginX => _originX;
    public int OriginY => _originY;

    public BoardState()
    {
        Cells = new CellData[MAX_SIZE * MAX_SIZE];

        InitializeBoard();
    }

    /// <summary>
    /// コピーコンストラクタ（メモリ未確保時用）
    /// </summary>
    public BoardState(BoardState source)
    {
        Cells = new CellData[MAX_SIZE * MAX_SIZE];
        Array.Copy(source.Cells, Cells, source.Cells.Length);

        Width = source.Width;
        Height = source.Height;
        _originX = source._originX;
        _originY = source._originY;
    }

    /// <summary>
    /// 既存の BoardState インスタンスに対して、自身の状態を上書き（同期）する
    /// </summary>
    public void CopyTo(BoardState dst)
    {
        // 配列のメモリブロックをまるごと転送
        Array.Copy(Cells, dst.Cells, Cells.Length);

        // 状態管理用のプリミティブ変数を同期
        dst.Width = Width;
        dst.Height = Height;
        dst._originX = _originX;
        dst._originY = _originY;
    }

    private void InitializeBoard(int initialSize = 8)
    {
        Width = initialSize;
        Height = initialSize;

        // 真ん中に配置されるようにオフセットを計算
        // 12x12 の中で 8x8 を使うなら、開始位置は (2, 2)
        _originX = (MAX_SIZE - initialSize) / 2;
        _originY = (MAX_SIZE - initialSize) / 2;

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
        // 論理座標チェック
        if (x < 0 || x >= Width || y < 0 || y >= Height)
            return new CellData { Color = StoneColor.Wall };

        int realX = _originX + x;
        int realY = _originY + y;

        if (realX < 0 || realX >= MAX_SIZE || realY < 0 || realY >= MAX_SIZE)
            throw new IndexOutOfRangeException("BoardState: Origin offset is invalid.");

        return Cells[realY * MAX_SIZE + realX];
    }

    public void SetCell(int x, int y, StoneColor c, StoneType t)
    {
        if (x < 0 || x >= Width || y < 0 || y >= Height)
            return;

        int realX = _originX + x;
        int realY = _originY + y;

        if (realX < 0 || realX >= MAX_SIZE || realY < 0 || realY >= MAX_SIZE)
            throw new IndexOutOfRangeException("BoardState: Origin offset is invalid.");

        int idx = realY * MAX_SIZE + realX;
        Cells[idx].Color = c;
        Cells[idx].Type = t;
    }

    /// <summary>
    /// 盤面拡張
    /// 配列の中身は一切動かさず、有効範囲を広げるだけ
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
