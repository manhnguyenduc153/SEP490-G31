namespace sep490_be.Enums
{
    /// <summary>
    /// Trạng thái yêu cầu hoàn trả hàng
    /// </summary>
    public enum ReturnRequestStatus
    {
        /// <summary>
        /// Chờ seller phản hồi
        /// </summary>
        Pending = 1,

        /// <summary>
        /// Seller chấp nhận, chờ buyer gửi hàng
        /// </summary>
        AwaitingBuyerShipment = 2,

        /// <summary>
        /// Seller từ chối yêu cầu
        /// </summary>
        Rejected = 3,

        /// <summary>
        /// Buyer đã gửi hàng trả
        /// </summary>
        ItemShipped = 4,

        /// <summary>
        /// Seller đã nhận được hàng trả
        /// </summary>
        ItemReceived = 5,

        /// <summary>
        /// Đã đóng (hoàn tất hoặc hủy)
        /// </summary>
        Closed = 6,

        /// <summary>
        /// Buyer hủy yêu cầu
        /// </summary>
        Cancelled = 7,

        /// <summary>
        /// Đang tranh chấp
        /// </summary>
        Disputed = 8
    }
}

