using MessagePipe;
using VContainer;
using VContainer.Unity;

namespace App.Reversi
{
    public class GameLifetimeScope : LifetimeScope
    {
        protected override void Configure(IContainerBuilder builder)
        {
            // Publisher
            builder.RegisterComponentInHierarchy<Board>();

            // Subscriber
            builder.RegisterComponentInHierarchy<ReversiManager>();

            MessagePipeOptions options = builder.RegisterMessagePipe();
            builder.RegisterMessageBroker<PutStoneInfo>(options);
            builder.RegisterMessageBroker<SelectedStoneTypeInfo>(options);
            builder.RegisterMessageBroker<BoardInfo>(options);
        }
    }
}
