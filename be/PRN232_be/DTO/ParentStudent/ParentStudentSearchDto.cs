using PRN232_be.DTO.Common;

namespace PRN232_be.DTO.ParentStudent
{
    public class ParentStudentSearchDto : BaseSearchDto
    {
        /// <summary>
        /// Lọc phụ huynh theo học sinh cụ thể
        /// </summary>
        public int? StudentId { get; set; }
    }
}
