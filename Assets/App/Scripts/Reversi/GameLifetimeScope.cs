using App.Reversi.Core;
using App.Reversi.Messaging;
using MessagePipe;
using VContainer;
using VContainer.Unity;

namespace App.Reversi
{
    public class GameLifetimeScope : LifetimeScope
    {
        protected override void Configure(IContainerBuilder builder)
        {
            // 階層（Hierarchy）から取得するコンポーネント
            builder.RegisterComponentInHierarchy<Board>();
            builder.RegisterComponentInHierarchy<UIManager>();

            // Coreコンポーネントを登録
            builder.RegisterComponentInHierarchy<InputManager>();
            builder.RegisterComponentInHierarchy<GameController>();
            builder.RegisterComponentInHierarchy<PlayerInventory>();
            builder.RegisterComponentInHierarchy<AudioManager>();

            // MessagePipeの設定
            MessagePipeOptions options = builder.RegisterMessagePipe();

            // メッセージ
            builder.RegisterMessageBroker<SelectedStoneTypeInfo>(options);
            builder.RegisterMessageBroker<BoardInfo>(options);

            builder.RegisterMessageBroker<CellClickedMessage>(options);
            builder.RegisterMessageBroker<RequestPutStoneMessage>(options);
            builder.RegisterMessageBroker<TurnChangedMessage>(options);
            builder.RegisterMessageBroker<AvailableCountChangedMessage>(options);
            builder.RegisterMessageBroker<GameOverMessage>(options);
            builder.RegisterMessageBroker<PlaySoundEffectMessage>(options);
        }
    }
}
