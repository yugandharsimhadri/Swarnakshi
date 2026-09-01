using Microsoft.Playwright;

namespace Swarnakshi.Automation.Workflows;

/// <summary>
/// Everything a workflow needs to drive the app, plus the navigation and verification helpers every
/// workflow would otherwise repeat. Verification uses Playwright's web-first assertions, which retry
/// until their timeout — so the same step works at test speed and at demo speed without waits
/// sprinkled through the scenarios.
/// </summary>
public sealed class WorkflowContext(IPage page, Narrator narrator, AutomationOptions options)
{
    public IPage Page { get; } = page;

    public Narrator Narrator { get; } = narrator;

    public AutomationOptions Options { get; } = options;

    public Viewport Viewport => Options.Viewport;

    public bool IsMobile => Options.Viewport == Viewport.Mobile;

    /// <summary>Narrates a step, then runs it. The caption is on screen before the action it describes.</summary>
    public async Task StepAsync(string narration, Func<Task> action)
    {
        await Narrator.SayAsync(narration);
        await action();
    }

    /// <summary>Narrates a beat with no interaction of its own — used to explain what is on screen.</summary>
    public Task SayAsync(string narration) => Narrator.SayAsync(narration);

    public static ILocatorAssertions Expect(ILocator locator) => Assertions.Expect(locator);

    public static IPageAssertions Expect(IPage page) => Assertions.Expect(page);

    /// <summary>
    /// The visible one of a set of matches.
    ///
    /// Filtering to visible before First() is the single most important helper here. Swarnakshi
    /// ships BOTH layouts in the DOM and hides one with a breakpoint — the master screens render a
    /// desktop table inside <c>hidden lg:block</c> and mobile cards inside <c>lg:hidden</c>, so every
    /// material name, contractor name and action button exists twice on every master screen. A plain
    /// First() therefore binds to whichever copy comes first in the DOM, which on desktop is the
    /// hidden mobile card — and then waits out its timeout for a visibility that will never come.
    /// Filtering first asks the question the check actually means: can the user see this.
    /// </summary>
    public static ILocator Visible(ILocator locator)
        => locator.Filter(new LocatorFilterOptions { Visible = true }).First;

    /// <summary>Asserts a piece of text is visible on screen.</summary>
    public Task ExpectVisibleAsync(string text)
        => Expect(Visible(Page.GetByText(text))).ToBeVisibleAsync();

    /// <summary>Asserts the page heading, which is how every screen in this product announces itself.</summary>
    public Task ExpectHeadingAsync(string heading)
        => Expect(Page.GetByRole(AriaRole.Heading, new() { Name = heading, Exact = true }).First)
            .ToBeVisibleAsync();

    /// <summary>
    /// Clicks a button INSIDE the open confirmation dialog.
    ///
    /// Scoped to the dialog deliberately: the confirm dialogs reuse the same verb as the row action
    /// that opened them ("Deactivate" confirms "Deactivate"), so an unscoped lookup finds the row
    /// button first — which is now behind the modal overlay, and therefore never becomes clickable.
    /// The run then fails as "element not stable" rather than as the ambiguity it really is.
    /// </summary>
    public Task ConfirmAsync(string label)
        => Visible(Page.GetByRole(AriaRole.Dialog)
            .GetByRole(AriaRole.Button, new() { Name = label, Exact = true })).ClickAsync();

    /// <summary>
    /// Opens a master record's detail view from its row.
    ///
    /// A real difference between the layouts, not a selector quirk: the desktop table gives each row
    /// an explicit "View" action, while the mobile card has no such button — the card itself is the
    /// control, and its inline buttons are only Edit and Deactivate.
    /// </summary>
    public Task OpenDetailAsync(string rowText)
        => IsMobile
            ? Row(rowText).ClickAsync()
            : Button("View").ClickAsync();

    /// <summary>
    /// The one visible row carrying this text, in whichever layout is currently on screen.
    ///
    /// Both layouts ship in the DOM at once, and the desktop table's wrapper is ALSO a
    /// <c>div.rounded-2xl</c> that contains every row's text — so on mobile an unfiltered container
    /// match binds to the HIDDEN table rather than to the card, and then waits out its timeout for a
    /// control that can never become visible. The visibility filter belongs on the CONTAINER, not
    /// only on the control reached through it.
    /// </summary>
    private ILocator Row(string rowText)
    {
        var rows = IsMobile ? Page.Locator("div.rounded-2xl") : Page.Locator("tr");
        return Visible(rows.Filter(new LocatorFilterOptions { HasText = rowText }));
    }

    /// <summary>
    /// A row's own action button, scoped to the row that carries <paramref name="rowText"/>.
    ///
    /// Never reach for a row action with a bare button lookup. Every row offers the same verbs, so
    /// an unscoped locator takes the FIRST one on screen — and if the list is not filtered to the
    /// record you mean (or has not finished filtering), that is somebody else's row. This is not
    /// hypothetical: it deactivated a seeded material instead of the one the scenario created, and
    /// the run then failed several steps later looking for a record that was never touched.
    /// </summary>
    public ILocator RowAction(string rowText, string action)
        => Visible(Row(rowText).GetByRole(AriaRole.Button, new() { Name = action }));

    /// <summary>
    /// Types a search term and waits for the list to actually be filtered by it.
    ///
    /// Asserting straight after typing is a trap: the term is debounced, so for a moment the list is
    /// still the unfiltered one — and a freshly created record is visible in THAT list too. The
    /// assertion passes for the wrong reason and the next step acts on the wrong row.
    /// </summary>
    public async Task SearchAsync(string placeholder, string term)
    {
        await FillPlaceholderAsync(placeholder, term);
        await SettleAsync();
    }

    /// <summary>
    /// Waits until the row carrying <paramref name="rowText"/> shows the given status.
    ///
    /// Used as the synchronisation point after a lifecycle action instead of a bare network wait:
    /// it asserts the business outcome the step claims ("it is now inactive") AND guarantees the
    /// list has caught up before the next step acts on it. Waiting on the network alone let a run
    /// continue while the row still showed its old state, so the following step operated on stale
    /// rows and failed somewhere unrelated.
    /// </summary>
    public Task ExpectRowStatusAsync(string rowText, string status)
        => Expect(Visible(Row(rowText).GetByText(status, new() { Exact = true }))).ToBeVisibleAsync();

    /// <summary>The visible button with this name.</summary>
    public ILocator Button(string name, bool exact = false)
        => Visible(Page.GetByRole(AriaRole.Button, new() { Name = name, Exact = exact }));

    /// <summary>The visible link with this name.</summary>
    /// <summary>
    /// The visible link with this name, matched as a SUBSTRING by default.
    ///
    /// Not exact, because nearly every link in this product decorates its label: the tab bar renders
    /// an icon beside the caption ("☰ More"), and the More / Reports / Stock cards append a "▸"
    /// chevron. The accessible name therefore includes those characters, and an exact match on the
    /// caption alone silently matches nothing at all.
    /// </summary>
    public ILocator Link(string name, bool exact = false)
        => Visible(Page.GetByRole(AriaRole.Link, new() { Name = name, Exact = exact }));

    /// <summary>The visible control navigating to a path ending in <paramref name="hrefSuffix"/>.</summary>
    public ILocator LinkTo(string hrefSuffix)
        => Visible(Page.Locator($"a[href$='{hrefSuffix}']"));

    /// <summary>
    /// Moves to one of the product's screens by clicking, never by typing a URL — a navigation that
    /// only works from the address bar is not one a site engineer has.
    ///
    /// Swarnakshi's shell is the same bottom tab bar at every width, carrying five destinations;
    /// everything else lives behind "More". So this is two taps for the secondary screens in BOTH
    /// viewports, which is why there is no mobile branch here — unlike most apps of this shape.
    /// </summary>
    public async Task NavigateAsync(string label, string expectedHeading)
    {
        if (TabBarLabels.Contains(label))
        {
            await Visible(Page.GetByRole(AriaRole.Navigation)
                .GetByRole(AriaRole.Link, new() { Name = label })).ClickAsync();
            await ExpectHeadingAsync(expectedHeading);
            return;
        }

        // "More" is itself a tab, so the first tap is found in the nav landmark; what it opens is a
        // page of cards, not a menu — so the second lookup must NOT be scoped to a nav, or it finds
        // nothing and waits out its timeout.
        await Visible(Page.GetByRole(AriaRole.Navigation)
            .GetByRole(AriaRole.Link, new() { Name = "More" })).ClickAsync();
        await ExpectHeadingAsync("More");

        await Visible(Page.GetByRole(AriaRole.Link, new() { Name = label })).ClickAsync();
        await ExpectHeadingAsync(expectedHeading);
    }

    /// <summary>
    /// The five destinations the bottom tab bar carries directly. Everything else is behind "More".
    /// From AppShell's nav — keep in step with it.
    /// </summary>
    private static readonly HashSet<string> TabBarLabels =
        new(StringComparer.Ordinal) { "Home", "Sites", "Projects", "Stock", "More" };

    /// <summary>
    /// Opens a master screen reached from the Stock hub (Site Inventory, Material Master, …).
    /// These are cards on the /stock page rather than nav entries, in both viewports.
    /// </summary>
    public async Task OpenFromStockHubAsync(string cardLabel, string expectedHeading)
    {
        await NavigateAsync("Stock", "Stock");
        await Visible(Page.GetByRole(AriaRole.Link, new() { Name = cardLabel })).ClickAsync();
        await ExpectHeadingAsync(expectedHeading);
    }

    /// <summary>
    /// The control inside the Field whose caption is exactly <paramref name="caption"/>.
    ///
    /// Not GetByLabel, and the reason is specific to this app's Field component: it renders
    /// <c>&lt;label&gt;&lt;span&gt;caption&lt;/span&gt;{control}&lt;/label&gt;</c>, i.e. the control
    /// sits INSIDE the label. An implicit label's accessible name is its whole text content — which
    /// for a &lt;select&gt; includes every option's text. So GetByLabel("Category *", exact) can
    /// never match a select: the real label text is "Category *Select category…Cement Sand…".
    /// Matching the caption span exactly, then reaching into it for the control, is what actually
    /// identifies the field the user sees.
    /// </summary>
    public ILocator Field(string caption, string tag)
        => Visible(Page.Locator($"label:has(> span:text-is(\"{caption}\"))").Locator(tag));

    /// <summary>Types into the labelled text field with this caption.</summary>
    public Task FillAsync(string caption, string value)
        => Field(caption, "input").FillAsync(value);

    /// <summary>Chooses an option in the labelled select with this caption.</summary>
    public Task SelectAsync(string caption, string optionLabel)
        => Field(caption, "select").SelectOptionAsync(new SelectOptionValue { Label = optionLabel });

    /// <summary>
    /// Picks the first real option in a select — the first that is not the empty "Select …" prompt.
    ///
    /// Waits for that option to exist before reading it. These lists are filled from an API response
    /// after first paint, so reading immediately finds only the prompt and reports an empty list,
    /// which looks like unseeded data rather than a race.
    /// </summary>
    public async Task SelectFirstRealOptionAsync(ILocator select)
    {
        var target = Visible(select);
        await Expect(target.Locator("option").Nth(1)).ToBeAttachedAsync();

        var values = await target.Locator("option").EvaluateAllAsync<string[]>(
            "options => options.map(o => o.value).filter(v => v !== '')");

        if (values.Length == 0)
            throw new InvalidOperationException(
                "The select still offers no real option after waiting — is the demo data seeded?");

        await target.SelectOptionAsync(values[0]);
    }

    /// <summary>
    /// Types into a field identified by its placeholder.
    ///
    /// Needed because the search boxes and filter rows are NOT wrapped in the Field component — they
    /// are bare inputs whose only caption is the placeholder. GetByLabel finds nothing for them.
    /// </summary>
    public Task FillPlaceholderAsync(string placeholder, string value)
        => Visible(Page.GetByPlaceholder(placeholder)).FillAsync(value);

    /// <summary>
    /// The visible select that offers a given option — the honest way to reach the filter rows,
    /// which are unlabelled selects distinguishable only by what they contain.
    /// </summary>
    public ILocator SelectHavingOption(string optionLabel)
        => Visible(Page.Locator($"select:has(option:text-is(\"{optionLabel}\"))"));

    /// <summary>Chooses an option in an unlabelled filter select, found by another option it offers.</summary>
    public Task SelectFilterAsync(string knownOptionLabel, string chooseLabel)
        => SelectHavingOption(knownOptionLabel)
            .SelectOptionAsync(new SelectOptionValue { Label = chooseLabel });

    /// <summary>
    /// A row in whichever master layout is on screen. On desktop that is a table row; on mobile it
    /// is a card. Both carry the record's name, so this finds the row by the text the user reads.
    /// </summary>
    public ILocator RowContaining(string text)
        => Visible(Page.Locator("tr, div").Filter(new LocatorFilterOptions { HasText = text }));

    /// <summary>
    /// Waits for the screen to stop moving before the next interaction.
    ///
    /// These lists re-render on every request they make, and a lifecycle action fires three at once
    /// (the list, the summary and the brand/type lookup) on top of the POST that triggered them. A
    /// row located during that burst passes Playwright's stability check and is then replaced before
    /// the click lands — reported as "element was detached from the DOM, retrying" until the action
    /// times out. Waiting for the network to go quiet first is what makes the next click land on a
    /// row that will still be there.
    /// </summary>
    public Task SettleAsync() => Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

    /// <summary>
    /// Scrolls the desktop table sideways so its Actions column is on screen.
    ///
    /// Needed because the app shell is <c>max-w-md</c> at EVERY width — the product stays a
    /// phone-width column even on a 1440px display — while the master table is <c>min-w-[54rem]</c>
    /// inside an <c>overflow-x-auto</c>. The row's View / Edit / Deactivate controls are therefore
    /// always outside the visible area until the table is scrolled, on desktop as much as on a
    /// phone. A user has to do this too; the scenario should not pretend otherwise.
    /// </summary>
    public async Task RevealRowActionsAsync()
    {
        if (IsMobile) return;   // mobile cards carry their actions inline, nothing to scroll
        await Page.EvaluateAsync(
            "() => { const t = document.querySelector('div.overflow-x-auto'); if (t) t.scrollLeft = t.scrollWidth; }");
    }

    /// <summary>A deliberate pause for the camera. Skipped under test.</summary>
    public Task BeatAsync(int milliseconds = 600) => Narrator.BeatAsync(milliseconds);
}
