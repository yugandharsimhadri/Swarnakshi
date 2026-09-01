using System.Text.RegularExpressions;
using Microsoft.Playwright;

namespace Swarnakshi.Automation.Workflows;

/// <summary>
/// The way in, and the way everyone else is kept out. Runs from an already-signed-in state (as every
/// workflow does) by signing out first, so it can show the whole door: a refused credential and then
/// a successful sign-in.
/// </summary>
public sealed class SignInWorkflow() : Workflow(
    key: "SignIn",
    displayName: "Signing In",
    module: "Security",
    businessPurpose: "Put every screen behind a named login, so a site supervisor can enter the day's "
        + "material movements without seeing the company's money — and every entry is tied to whoever made it.")
{
    public override async Task RunAsync(WorkflowContext c)
    {
        await c.StepAsync(
            "Signing out returns to the sign-in screen — the only way into the product.",
            async () =>
            {
                await c.NavigateAsync("More", "More");
                await c.Button("Sign out").ClickAsync();
                await c.ExpectVisibleAsync("Construction Expense & Inventory");
            });

        await c.StepAsync(
            "A wrong password is refused, and says only that the credentials are wrong.",
            async () =>
            {
                await c.FillAsync("Username", DemoData.OwnerLogin);
                await c.FillAsync("Password", "not-the-password");
                await c.Button("Sign in").ClickAsync();
                await c.ExpectVisibleAsync("Invalid username or password.");
            });

        await c.StepAsync(
            "The right password lets the owner through to the dashboard.",
            async () =>
            {
                await c.FillAsync("Password", DemoData.OwnerPassword);
                await c.Button("Sign in").ClickAsync();
                // Login is a client-side route change with no load event, so arrival is asserted on
                // the app shell's own navigation rather than on any one figure.
                await WorkflowContext.Expect(c.Page.GetByRole(AriaRole.Navigation).First)
                    .ToBeVisibleAsync(new() { Timeout = 60_000 });
            });

        await c.StepAsync(
            "Back inside, the dashboard greets the signed-in user by name.",
            async () =>
            {
                // Signing in returns the user to the route they were on — here /more, because that
                // is where Sign out was pressed — rather than bouncing them to the dashboard. So the
                // greeting is reached by navigating, exactly as the user would.
                await c.NavigateAsync("Home", $"Hi, {DemoData.OwnerName}");
            });
    }
}

/// <summary>
/// What the owner sees before opening anything. The first screen answers the questions a builder
/// asks each morning — how many projects are running, what stock is on site and unspent, and how
/// much the customers still owe — so the day starts from numbers rather than from a phone call.
/// </summary>
public sealed class DashboardWorkflow() : Workflow(
    key: "Dashboard",
    displayName: "The Morning View",
    module: "Overview",
    businessPurpose: "Answer the day's first questions — projects running, stock held, money owed — "
        + "on one screen, before any record is opened.")
{
    public override async Task RunAsync(WorkflowContext c)
    {
        await c.StepAsync(
            "The dashboard opens on the state of the business.",
            async () =>
            {
                await c.NavigateAsync("Home", $"Hi, {DemoData.OwnerName}");
                await c.ExpectVisibleAsync("Projects");
                await c.ExpectVisibleAsync("Active sites");
            });

        await c.StepAsync(
            "Inventory value and project cost are shown apart, because they are different money: "
                + "stock bought is not yet a project's cost until it is consumed.",
            async () =>
            {
                await c.ExpectVisibleAsync("Inventory value");
                await c.ExpectVisibleAsync("Project cost (all)");
            });

        await c.StepAsync(
            "What customers still owe is on the same screen as what the sites hold.",
            () => c.ExpectVisibleAsync("Customer receivable"));

        await c.StepAsync(
            "Reports are one tap from here.",
            () => c.ExpectVisibleAsync("Reports"));
    }
}

/// <summary>
/// Who in the business can reach what. Swarnakshi's roles are a hierarchy, and the screens carrying
/// authority — approvals and user administration above all — are reachable only by the roles that
/// hold it. This shows the owner's view; the backend suite proves the other roles are refused
/// server-side, which is where it actually matters.
/// </summary>
public sealed class UserAccessWorkflow() : Workflow(
    key: "UserAccess",
    displayName: "Staff Accounts And Access",
    module: "Security",
    businessPurpose: "Give site staff their own logins without giving them the company's money "
        + "screens, and keep approval authority with the owner.")
{
    public override async Task RunAsync(WorkflowContext c)
    {
        await c.StepAsync(
            "The owner reaches user administration from More.",
            () => c.NavigateAsync("Users", "Users"));

        await c.StepAsync(
            "The seeded owner account is listed with its role.",
            async () =>
            {
                // The list identifies a user by display name and role. It deliberately does not
                // print the login: since multi-tenancy that is username@companycode, and the
                // company half is the same for everyone on the screen.
                await c.ExpectVisibleAsync(DemoData.OwnerName);
            });

        await c.StepAsync(
            "The Approval Centre — where money and stock decisions are made — is also owner-only.",
            () => c.NavigateAsync("Approval Center", "Approval Center"));
    }
}
