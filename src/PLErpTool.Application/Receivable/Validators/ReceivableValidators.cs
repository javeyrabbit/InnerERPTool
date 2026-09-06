using FluentValidation;
using PLErpTool.Application.Receivable.Commands;

namespace PLErpTool.Application.Receivable.Validators;

public sealed class RegisterPaymentValidator : AbstractValidator<RegisterPaymentCommand>
{
    public RegisterPaymentValidator()
    {
        RuleFor(x => x.MonthlyDebtId).GreaterThan(0);
        RuleFor(x => x.Amount).GreaterThan(0m).WithMessage("金额必须大于 0");
        RuleFor(x => x.Remark).MaximumLength(200);
    }
}

public sealed class RecordDebtValidator : AbstractValidator<RecordDebtCommand>
{
    public RecordDebtValidator()
    {
        RuleFor(x => x.CustomerId).GreaterThan(0);
        RuleFor(x => x.Amount).GreaterThanOrEqualTo(0m);
        RuleFor(x => x.DebtType)
            .Must(type => type is "其他欠款" or "退货")
            .WithMessage("请选择欠款类型：其他欠款或退货");
    }
}
