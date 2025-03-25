using VContainer;
using VContainer.Unity;

public class GameLifetimeScope : LifetimeScope
{
    protected override void Configure(IContainerBuilder builder)
    {
        builder.RegisterComponentInHierarchy<GameManager>();
        builder.RegisterComponentInHierarchy<UIManager>();
        builder.RegisterComponentInHierarchy<ReversiManager>();
    }
}
