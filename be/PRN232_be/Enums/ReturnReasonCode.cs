namespace PRN232_be.Enums
{
    /// <summary>
    /// Mã lý do yêu cầu hoàn trả hàng
    /// </summary>
    public enum ReturnReasonCode
    {
        /// <summary>
        /// Sản phẩm không đúng mô tả
        /// </summary>
        ItemNotAsDescribed = 1,

        /// <summary>
        /// Sản phẩm bị hỏng khi nhận
        /// </summary>
        ItemDamaged = 2,

        /// <summary>
        /// Gửi sai sản phẩm
        /// </summary>
        WrongItem = 3,

        /// <summary>
        /// Sản phẩm bị lỗi/không hoạt động
        /// </summary>
        ItemDefective = 4,

        /// <summary>
        /// Thiếu phụ kiện/linh kiện
        /// </summary>
        MissingParts = 5,

        /// <summary>
        /// Đổi ý (nếu seller cho phép)
        /// </summary>
        ChangedMind = 6,

        /// <summary>
        /// Lý do khác
        /// </summary>
        Other = 99,

        /// <summary>
        /// Người mua yêu cầu hủy đơn hàng
        /// </summary>
        OrderCancellation = 7
    }
}
