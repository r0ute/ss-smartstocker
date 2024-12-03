using Xunit;
using SS.src;

namespace SS.test;

public class PluginTest
{
    [Fact]
    public void ShouldWork()
    {
        Plugin.StockManager.AutoStockProducts();
        Assert.Equal(1, 1);
    }
}