namespace PRN232_be.Enums
{
    public enum DisputeType
    {
        INR,    // Item Not Received
        INAD,   // Item Not as Described
        OTHER
    }

    public enum DisputeStatus
    {
        Open,
        WaitingSeller,
        Escalated,
        UnderReview,
        ResolvedRefund,
        ResolvedNoRefund,
        Closed,
        Cancelled
    }

    public enum DisputeRefundStatus
    {
        Pending,
        Processed,
        Declined
    }
}