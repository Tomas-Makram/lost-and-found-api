namespace DataLayer.Models
{
    // Wallet transaction type
    public enum WalletTxnType
    {
        Bonus = 1,
        Fine = 2,
        PaymentCredit = 3,
        PaymentDebit = 4,
        Refund = 5,
        Adjustment = 6,
        Hold = 7,
        ReleaseHold = 8
    }
}
