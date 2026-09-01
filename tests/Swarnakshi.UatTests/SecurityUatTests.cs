using Swarnakshi.Automation;
using Xunit.Abstractions;

namespace Swarnakshi.UatTests;

/// <summary>
/// Getting into Swarnakshi, and being kept out of it. Every screen sits behind a named login, so a
/// builder can give a site supervisor the day's material entry without handing over the company's
/// money screens. A wrong password says only that it was wrong.
/// </summary>
public sealed class SignInUatTests(UatFixture fixture, ITestOutputHelper output) : UatTestBase(fixture, output)
{
    [Theory]
    [MemberData(nameof(BothViewports))]
    public Task Signing_in_and_being_refused(Viewport viewport) => RunWorkflowAsync("SignIn", viewport);
}

/// <summary>
/// The first screen of the working day. A builder opens Swarnakshi to see where the business stands
/// before opening any record — projects running, stock held and unspent, money still owed.
/// </summary>
public sealed class DashboardUatTests(UatFixture fixture, ITestOutputHelper output) : UatTestBase(fixture, output)
{
    [Theory]
    [MemberData(nameof(BothViewports))]
    public Task The_morning_view_of_the_business(Viewport viewport) => RunWorkflowAsync("Dashboard", viewport);
}

/// <summary>
/// Who in the business can reach what. Approval authority and user administration stay with the
/// owner; the backend suite proves the other roles are refused server-side, which is where a
/// determined user would try.
/// </summary>
public sealed class UserAccessUatTests(UatFixture fixture, ITestOutputHelper output) : UatTestBase(fixture, output)
{
    [Theory]
    [MemberData(nameof(BothViewports))]
    public Task Staff_accounts_and_the_screens_they_reach(Viewport viewport) => RunWorkflowAsync("UserAccess", viewport);
}
