using FluentAssertions;
using Swarnakshi.Domain.Entities;
using Xunit;

namespace Swarnakshi.Tests;

public class InventoryBalanceTests
{
    private static readonly DateTimeOffset T = DateTimeOffset.UtcNow;

    [Fact]
    public void Weighted_average_after_two_receipts()
    {
        var b = new InventoryBalance();
        b.Receive(100, 400, T);
        b.Receive(100, 450, T);

        b.Quantity.Should().Be(200);
        b.AverageRate.Should().Be(425);
        b.Value.Should().Be(200 * 425);
    }

    [Fact]
    public void Issue_uses_current_average_and_reduces_value()
    {
        var b = new InventoryBalance();
        b.Receive(100, 400, T);
        b.Receive(200, 450, T); // avg = (40000 + 90000) / 300 = 433.33...

        var rate = b.Issue(60, T, allowNegative: false);

        rate.Should().BeApproximately(433.33m, 0.01m);
        b.Quantity.Should().Be(240);
    }

    [Fact]
    public void No_double_counting_purchase_equals_consumed_plus_remaining()
    {
        var b = new InventoryBalance();
        b.Receive(100, 400, T);
        b.Receive(100, 450, T);
        var purchaseValue = 100 * 400m + 100 * 450m; // 85_000

        var rate = b.Issue(50, T, allowNegative: false);
        var consumedCost = 50 * rate;

        (consumedCost + b.Value).Should().BeApproximately(purchaseValue, 0.01m);
    }

    [Fact]
    public void Issue_beyond_stock_throws_when_negative_not_allowed()
    {
        var b = new InventoryBalance();
        b.Receive(10, 100, T);

        var act = () => b.Issue(20, T, allowNegative: false);

        act.Should().Throw<InvalidOperationException>().WithMessage("*Insufficient*");
    }

    [Fact]
    public void Issue_beyond_stock_allowed_when_negative_permitted()
    {
        var b = new InventoryBalance();
        b.Receive(10, 100, T);

        b.Issue(20, T, allowNegative: true);

        b.Quantity.Should().Be(-10);
    }
}
