namespace ShoeStore.Infrastructure
{
    // Địa chỉ được lưu thành 1 chuỗi: "số nhà, tên đường, Phường/Xã ..., Tỉnh/Thành phố ..."
    // Dùng chung cho trang Đặt hàng và trang Tài khoản để ghép / tách địa chỉ giống nhau.
    public static class AddressFormatter
    {
        // Ghép 3 phần thành chuỗi đầy đủ
        public static string Join(string street, string wardName, string provinceName)
            => $"{street}, {wardName}, {provinceName}";

        // Tách ngược chuỗi đã lưu để chọn sẵn tỉnh, phường trên form.
        // Trả về false nếu địa chỉ không theo mẫu mới (vd: nhập tay, hoặc địa chỉ cũ còn quận/huyện)
        public static bool TrySplit(string? address, out string province, out string ward, out string street)
        {
            province = ward = street = string.Empty;

            if (string.IsNullOrWhiteSpace(address))
            {
                return false;
            }

            var parts = address.Split(", ");
            if (parts.Length < 3)
            {
                return false;
            }

            var lastPart = parts[^1];
            var wardPart = parts[^2];

            var looksLikeNewFormat =
                (lastPart.StartsWith("Tỉnh ") || lastPart.StartsWith("Thành phố ")) &&
                (wardPart.StartsWith("Phường ") || wardPart.StartsWith("Xã ") || wardPart.StartsWith("Đặc khu "));

            if (!looksLikeNewFormat)
            {
                return false;
            }

            province = lastPart;
            ward = wardPart;
            street = string.Join(", ", parts[..^2]);
            return true;
        }
    }
}
