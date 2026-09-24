// プレイヤーの磁力で掴める敵が実装する。MagnetPull が掴み・離し・吹っ飛ばしを通知する
public interface IMagnetEnemy
{
    // 掴んだ時に敵用の持ち方(手の前で向き合わせて持つ)をするか。
    // false なら箱などの物体と同じく手元にそのまま持つ
    bool HoldAsEnemy { get; }

    void OnMagnetGrabbed();  // 引き寄せ開始(掴まれた)
    void OnMagnetAttached(); // 引き寄せ終わって手元に届いた
    void OnMagnetReleased(); // ZRを離してそっと離された
    void OnMagnetRepelled(); // 極切替で吹っ飛ばされた
}
