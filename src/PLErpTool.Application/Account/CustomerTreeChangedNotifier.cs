namespace PLErpTool.Application.Account;

public interface ICustomerTreeChangedNotifier
{
    event EventHandler? TreeChanged;

    void NotifyChanged();
}

public sealed class CustomerTreeChangedNotifier : ICustomerTreeChangedNotifier
{
    public event EventHandler? TreeChanged;

    public void NotifyChanged() => TreeChanged?.Invoke(this, EventArgs.Empty);
}