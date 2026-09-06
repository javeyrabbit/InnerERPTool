using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;

namespace PLErpTool.Application.Receivable.Dtos;

public sealed class DetailPageNumberItem
{
    public DetailPageNumberItem(string label, int page, bool isCurrent)
    {
        Label = label;
        Page = page;
        IsCurrent = isCurrent;
    }

    public string Label { get; }
    public int Page { get; }
    public bool IsCurrent { get; }
}

public sealed class MonthlyDebtDto : INotifyPropertyChanged
{
    private const int SubPageSize = 10;

    public long Id { get; }
    public long CustomerId { get; }
    public string CustomerName { get; }
    public long? SalesmanId { get; }
    public string SalesmanName { get; }
    public string Month { get; }

    private readonly List<PaymentDto> _allDebtPayments;
    private readonly List<PaymentDto> _allCollectionPayments;

    public ObservableCollection<PaymentDto> DebtPayments { get; } = new();
    public ObservableCollection<PaymentDto> CollectionPayments { get; } = new();
    public ObservableCollection<DetailPageNumberItem> DebtPageNumbers { get; } = new();
    public ObservableCollection<DetailPageNumberItem> CollectionPageNumbers { get; } = new();
    public ObservableCollection<string> DebtProductSuggestions { get; } = new();
    public ObservableCollection<string> CustomerMaterialSuggestions { get; } = new();
    public ObservableCollection<string> DebtTypeOptions { get; } = new(new[] { "其他欠款", "退货" });

    private string _debtProductKeyword = string.Empty;
    public string DebtProductKeyword
    {
        get => _debtProductKeyword;
        set
        {
            var keyword = value ?? string.Empty;
            if (_debtProductKeyword == keyword) return;
            _debtProductKeyword = keyword;
            _customerMaterialKeyword = string.Empty;
            DebtCurrentPage = 1;
            RefreshDebtProductSuggestions();
            RefreshCustomerMaterialSuggestions();
            RefreshDebtPage();
            OnPropertyChanged();
            OnPropertyChanged(nameof(CustomerMaterialKeyword));
            OnPropertyChanged(nameof(HasDebtProductFilter));
        }
    }

    private string _customerMaterialKeyword = string.Empty;
    public string CustomerMaterialKeyword
    {
        get => _customerMaterialKeyword;
        set
        {
            var keyword = value ?? string.Empty;
            if (_customerMaterialKeyword == keyword) return;
            _customerMaterialKeyword = keyword;
            DebtCurrentPage = 1;
            RefreshDebtPage();
            OnPropertyChanged();
        }
    }

    public bool HasDebtProductFilter => !string.IsNullOrWhiteSpace(DebtProductKeyword);

    private string? _debtTypeKeyword;
    public string? DebtTypeKeyword
    {
        get => _debtTypeKeyword;
        set
        {
            var keyword = string.IsNullOrWhiteSpace(value) ? null : value;
            if (_debtTypeKeyword == keyword) return;
            _debtTypeKeyword = keyword;
            DebtCurrentPage = 1;
            RefreshDebtPage();
            OnPropertyChanged();
        }
    }

    private int _debtCurrentPage = 1;
    public int DebtCurrentPage
    {
        get => _debtCurrentPage;
        private set
        {
            if (_debtCurrentPage == value) return;
            _debtCurrentPage = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasDebtPrevPage));
            OnPropertyChanged(nameof(HasDebtNextPage));
        }
    }

    private int _collectionCurrentPage = 1;
    public int CollectionCurrentPage
    {
        get => _collectionCurrentPage;
        private set
        {
            if (_collectionCurrentPage == value) return;
            _collectionCurrentPage = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasCollectionPrevPage));
            OnPropertyChanged(nameof(HasCollectionNextPage));
        }
    }

    public int DebtTotalPages => GetTotalPages(GetFilteredDebtPayments().Count);
    public int DebtTotalCount => GetFilteredDebtPayments().Count;
    public bool HasDebtPrevPage => DebtCurrentPage > 1;
    public bool HasDebtNextPage => DebtCurrentPage < DebtTotalPages;

    public int CollectionTotalPages => GetTotalPages(_allCollectionPayments.Count);
    public int CollectionTotalCount => _allCollectionPayments.Count;
    public bool HasCollectionPrevPage => CollectionCurrentPage > 1;
    public bool HasCollectionNextPage => CollectionCurrentPage < CollectionTotalPages;

    public ICommand DebtPrevPageCommand { get; }
    public ICommand DebtNextPageCommand { get; }
    public ICommand DebtGoToPageCommand { get; }
    public ICommand ClearDebtProductFilterCommand { get; }
    public ICommand CollectionPrevPageCommand { get; }
    public ICommand CollectionNextPageCommand { get; }
    public ICommand CollectionGoToPageCommand { get; }

    private decimal _debtAmount;
    public decimal DebtAmount
    {
        get => _debtAmount;
        set
        {
            if (_debtAmount != value) { _debtAmount = value; OnPropertyChanged(); OnPropertyChanged(nameof(Balance)); }
        }
    }

    private decimal _collectedAmount;
    public decimal CollectedAmount
    {
        get => _collectedAmount;
        set
        {
            if (_collectedAmount != value) { _collectedAmount = value; OnPropertyChanged(); OnPropertyChanged(nameof(Balance)); }
        }
    }

    private decimal _balance;
    public decimal Balance
    {
        get => _collectedAmount - _debtAmount;
        set
        {
            if (_balance != value) { _balance = value; OnPropertyChanged(); }
        }
    }

    public MonthlyDebtDto(
        long id, long customerId, string customerName,
        long? salesmanId, string salesmanName, string month,
        decimal debtAmount, decimal collectedAmount, decimal balance,
        List<PaymentDto> payments)
    {
        Id = id; CustomerId = customerId; CustomerName = customerName;
        SalesmanId = salesmanId; SalesmanName = salesmanName; Month = month;
        _debtAmount = debtAmount; _collectedAmount = collectedAmount; _balance = balance;
        _allDebtPayments = payments.Where(p => p.Type == "欠款").ToList();
        _allCollectionPayments = payments.Where(p => p.Type == "收款").ToList();
        DebtPrevPageCommand = new RelayCommand(DebtPrevPage);
        DebtNextPageCommand = new RelayCommand(DebtNextPage);
        DebtGoToPageCommand = new RelayCommand<int>(DebtGoToPage);
        ClearDebtProductFilterCommand = new RelayCommand(ClearDebtProductFilter);
        CollectionPrevPageCommand = new RelayCommand(CollectionPrevPage);
        CollectionNextPageCommand = new RelayCommand(CollectionNextPage);
        CollectionGoToPageCommand = new RelayCommand<int>(CollectionGoToPage);
        RefreshDebtProductSuggestions();
        RefreshCustomerMaterialSuggestions();
        RefreshDebtPage();
        RefreshCollectionPage();
    }

    // 新增一条明细（登记收款后局部追加），并刷新子表分页
    public void AddPayment(PaymentDto payment)
    {
        var target = payment.Type == "欠款" ? _allDebtPayments : _allCollectionPayments;
        target.Add(payment);
        if (payment.Type == "欠款")
        {
            RefreshDebtProductSuggestions();
            RefreshCustomerMaterialSuggestions();
            RefreshDebtPage();
        }
        else
            RefreshCollectionPage();
    }

    // 移除一条明细（软删除后局部更新UI），并刷新子表分页
    public void RemovePayment(PaymentDto payment)
    {
        if (payment.Type == "欠款")
        {
            _allDebtPayments.Remove(payment);
            DebtCurrentPage = Math.Min(DebtCurrentPage, DebtTotalPages);
            RefreshDebtProductSuggestions();
            RefreshCustomerMaterialSuggestions();
            RefreshDebtPage();
        }
        else
        {
            _allCollectionPayments.Remove(payment);
            CollectionCurrentPage = Math.Min(CollectionCurrentPage, CollectionTotalPages);
            RefreshCollectionPage();
        }
    }

    private void DebtPrevPage()
    {
        if (DebtCurrentPage > 1) { DebtCurrentPage--; RefreshDebtPage(); }
    }

    private void DebtNextPage()
    {
        if (DebtCurrentPage < DebtTotalPages) { DebtCurrentPage++; RefreshDebtPage(); }
    }

    private void DebtGoToPage(int page)
    {
        if (page < 1 || page > DebtTotalPages || page == DebtCurrentPage) return;
        DebtCurrentPage = page;
        RefreshDebtPage();
    }

    private void ClearDebtProductFilter()
    {
        _debtProductKeyword = string.Empty;
        _customerMaterialKeyword = string.Empty;
        _debtTypeKeyword = null;
        DebtCurrentPage = 1;
        RefreshDebtProductSuggestions();
        RefreshCustomerMaterialSuggestions();
        RefreshDebtPage();

        // 即使筛选属性此前未被下拉框回写，也要通知控件清除自身的选中状态。
        OnPropertyChanged(nameof(DebtProductKeyword));
        OnPropertyChanged(nameof(CustomerMaterialKeyword));
        OnPropertyChanged(nameof(DebtTypeKeyword));
        OnPropertyChanged(nameof(HasDebtProductFilter));
    }

    private void CollectionPrevPage()
    {
        if (CollectionCurrentPage > 1) { CollectionCurrentPage--; RefreshCollectionPage(); }
    }

    private void CollectionNextPage()
    {
        if (CollectionCurrentPage < CollectionTotalPages) { CollectionCurrentPage++; RefreshCollectionPage(); }
    }

    private void CollectionGoToPage(int page)
    {
        if (page < 1 || page > CollectionTotalPages || page == CollectionCurrentPage) return;
        CollectionCurrentPage = page;
        RefreshCollectionPage();
    }

    private void RefreshDebtPage()
    {
        var filteredPayments = GetFilteredDebtPayments();
        if (DebtCurrentPage > GetTotalPages(filteredPayments.Count))
            DebtCurrentPage = 1;

        RefreshPage(filteredPayments, DebtPayments, DebtCurrentPage);
        OnPropertyChanged(nameof(DebtTotalCount));
        OnPropertyChanged(nameof(DebtTotalPages));
        OnPropertyChanged(nameof(HasDebtPrevPage));
        OnPropertyChanged(nameof(HasDebtNextPage));
        BuildPageNumbers(DebtPageNumbers, DebtCurrentPage, DebtTotalPages);
    }

    private List<PaymentDto> GetFilteredDebtPayments()
    {
        var productKeyword = DebtProductKeyword.Trim();
        var materialKeyword = CustomerMaterialKeyword.Trim();
        var debtTypeKeyword = DebtTypeKeyword?.Trim() ?? string.Empty;
        return _allDebtPayments
            .Where(payment => string.IsNullOrWhiteSpace(productKeyword)
                || payment.ProductName.Contains(productKeyword, StringComparison.OrdinalIgnoreCase))
            .Where(payment => string.IsNullOrWhiteSpace(materialKeyword)
                || payment.CustomerMaterial.Contains(materialKeyword, StringComparison.OrdinalIgnoreCase))
            .Where(payment => string.IsNullOrWhiteSpace(debtTypeKeyword)
                || string.Equals(payment.DebtType, debtTypeKeyword, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    private void RefreshDebtProductSuggestions()
    {
        var keyword = DebtProductKeyword.Trim();
        var suggestions = _allDebtPayments
            .Select(payment => payment.ProductName)
            .Where(name => !string.IsNullOrWhiteSpace(name)
                && (string.IsNullOrWhiteSpace(keyword)
                    || name.Contains(keyword, StringComparison.OrdinalIgnoreCase)))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(name => name)
            .ToList();

        DebtProductSuggestions.Clear();
        foreach (var suggestion in suggestions)
            DebtProductSuggestions.Add(suggestion);
    }

    private void RefreshCustomerMaterialSuggestions()
    {
        var productKeyword = DebtProductKeyword.Trim();
        var suggestions = string.IsNullOrWhiteSpace(productKeyword)
            ? new List<string>()
            : _allDebtPayments
                .Where(payment => payment.ProductName.Contains(productKeyword, StringComparison.OrdinalIgnoreCase))
                .Select(payment => payment.CustomerMaterial)
                .Where(material => !string.IsNullOrWhiteSpace(material))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(material => material)
                .ToList();

        CustomerMaterialSuggestions.Clear();
        foreach (var suggestion in suggestions)
            CustomerMaterialSuggestions.Add(suggestion);
    }

    private void RefreshCollectionPage()
    {
        RefreshPage(_allCollectionPayments, CollectionPayments, CollectionCurrentPage);
        OnPropertyChanged(nameof(CollectionTotalCount));
        OnPropertyChanged(nameof(CollectionTotalPages));
        OnPropertyChanged(nameof(HasCollectionPrevPage));
        OnPropertyChanged(nameof(HasCollectionNextPage));
        BuildPageNumbers(CollectionPageNumbers, CollectionCurrentPage, CollectionTotalPages);
    }

    private static void RefreshPage(IEnumerable<PaymentDto> source, ObservableCollection<PaymentDto> target, int currentPage)
    {
        target.Clear();
        foreach (var payment in source.Skip((currentPage - 1) * SubPageSize).Take(SubPageSize))
            target.Add(payment);
    }

    private static int GetTotalPages(int count) => Math.Max(1, (int)Math.Ceiling(count / (double)SubPageSize));

    private static void BuildPageNumbers(
        ObservableCollection<DetailPageNumberItem> target,
        int currentPage,
        int totalPages)
    {
        target.Clear();
        int start = Math.Max(1, currentPage - 2);
        int end = Math.Min(totalPages, currentPage + 2);

        if (start > 1)
            target.Add(new DetailPageNumberItem("…", 0, false));
        for (int page = start; page <= end; page++)
            target.Add(new DetailPageNumberItem(page.ToString(), page, page == currentPage));
        if (end < totalPages)
            target.Add(new DetailPageNumberItem("…", 0, false));
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([System.Runtime.CompilerServices.CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

public sealed class PaymentDto : INotifyPropertyChanged
{
    public long Id { get; }
    public long MonthlyDebtId { get; }
    public DateTime TradeDate { get; }
    public string Type { get; }
    public string Remark { get; }

    public string DocumentNo { get; }
    public string ProductName { get; }
    public string SpecDetails { get; }
    public string CustomerMaterial { get; }
    public string DebtType { get; }
    public string DisplayDebtType => string.IsNullOrWhiteSpace(DebtType) ? "—" : DebtType;
    private string _reconciliationRemark;
    public string ReconciliationRemark
    {
        get => _reconciliationRemark;
        set
        {
            var normalizedValue = value ?? string.Empty;
            if (_reconciliationRemark == normalizedValue) return;
            _reconciliationRemark = normalizedValue;
            OnPropertyChanged();
        }
    }
    public string ReceivableRemark { get; }
    public string DisplayReceivableRemark => string.IsNullOrWhiteSpace(ReceivableRemark) ? Remark : ReceivableRemark;

    private decimal _quantity;
    public decimal Quantity
    {
        get => _quantity;
        set
        {
            if (_quantity != value)
            {
                _quantity = value;
                OnPropertyChanged();
                RecalculateAmount();
            }
        }
    }

    private decimal _unitPrice;
    public decimal UnitPrice
    {
        get => _unitPrice;
        set
        {
            if (_unitPrice != value)
            {
                _unitPrice = value;
                OnPropertyChanged();
                RecalculateAmount();
            }
        }
    }

    private decimal _amount;
    public decimal Amount
    {
        get => _amount;
        set
        {
            if (_amount != value)
            {
                _amount = value;
                OnPropertyChanged();
            }
        }
    }

    // 欠款明细：可改数量/单价，金额由计算得出；收款记录：可改金额，数量/单价只读
    public bool IsEditableQtyPrice => Type == "欠款";
    public bool IsEditableAmount => Type == "收款";

    public PaymentDto(
        long id, long monthlyDebtId, DateTime tradeDate, string type,
        decimal quantity, decimal unitPrice, decimal amount, string remark,
        string documentNo = "", string productName = "", string specDetails = "",
        string customerMaterial = "", string reconciliationRemark = "", string receivableRemark = "",
        string debtType = "")
    {
        Id = id;
        MonthlyDebtId = monthlyDebtId;
        TradeDate = tradeDate;
        Type = type;
        Remark = remark;
        DocumentNo = documentNo;
        ProductName = productName;
        SpecDetails = specDetails;
        CustomerMaterial = customerMaterial;
        DebtType = debtType;
        _reconciliationRemark = reconciliationRemark;
        ReceivableRemark = receivableRemark;
        _quantity = quantity;
        _unitPrice = unitPrice;
        _amount = amount;
    }

    // 数量/单价变更后重算金额（仅欠款明细）
    private void RecalculateAmount()
    {
        if (Type == "欠款")
            Amount = Math.Round(_quantity * _unitPrice, 2, MidpointRounding.AwayFromZero);
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([System.Runtime.CompilerServices.CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

public sealed class CustomerTreeNodeDto : INotifyPropertyChanged
{
    public long Id { get; }
    public string Name { get; }
    public bool IsCompany { get; }
    public List<CustomerTreeNodeDto> Children { get; }

    private bool _isSelected;
    private bool _isVisible = true;

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected != value)
            {
                _isSelected = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected)));
            }
        }
    }

    public bool IsVisible
    {
        get => _isVisible;
        set
        {
            if (_isVisible != value)
            {
                _isVisible = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsVisible)));
            }
        }
    }

    public CustomerTreeNodeDto(long id, string name, bool isCompany, List<CustomerTreeNodeDto> children)
    {
        Id = id;
        Name = name;
        IsCompany = isCompany;
        Children = children;
    }

    public event PropertyChangedEventHandler? PropertyChanged;
}
