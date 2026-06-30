using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ProflowApp.Models;

public class User
{
    [Key] public string UserID { get; set; } = "";

    [Required(ErrorMessage = "Nama wajib diisi")]
    [MinLength(3, ErrorMessage = "Nama minimal 3 karakter")]
    public string Nama { get; set; } = "";

    [Required(ErrorMessage = "Username wajib diisi")]
    [MinLength(3, ErrorMessage = "Username minimal 3 karakter")]
    [RegularExpression(@"^\S+$", ErrorMessage = "Username tidak boleh mengandung spasi")]
    public string Username { get; set; } = "";

    [Required(ErrorMessage = "Password wajib diisi")]
    [MinLength(6, ErrorMessage = "Password minimal 6 karakter")]
    public string? Password { get; set; } = "";

    public string Role { get; set; } = "";
    public bool IsDeleted { get; set; } = false;
    public int LoginAttempts { get; set; } = 0;
    public DateTime? LockedUntil { get; set; }
}

public class Barang
{
    [Key] public int Brg_ID { get; set; }

    [Required(ErrorMessage = "Nama barang wajib diisi.")]
    [MinLength(3, ErrorMessage = "Nama barang minimal 3 karakter.")]
    public string Nm_Brg { get; set; } = "";

    [Required(ErrorMessage = "Satuan wajib diisi.")]
    public string Satuan { get; set; } = "";

    [Required(ErrorMessage = "Harga estimasi wajib diisi.")]
    [Range(1, double.MaxValue, ErrorMessage = "Harga estimasi harus lebih dari 0.")]
    public decimal? Hrg_Est { get; set; }

    [Required(ErrorMessage = "Nama vendor wajib diisi.")]
    [MinLength(3, ErrorMessage = "Nama vendor minimal 3 karakter.")]
    public string Nm_Vendor { get; set; } = "";

    public bool IsDeleted { get; set; } = false;
}

public class Pengajuan
{
    [Key] public int PR_ID { get; set; }
    public string NoPR { get; set; } = "";
    public string UserID { get; set; } = "";
    
    [Required(ErrorMessage = "Pilih barang yang ingin diajukan.")]
    public int Brg_ID { get; set; }

    [Required(ErrorMessage = "Jumlah wajib diisi.")]
    [Range(1, 9999, ErrorMessage = "Jumlah harus antara 1 dan 9999.")]
    public int Jml { get; set; }

    public int? Status { get; set; }
    public DateTime Tgl_Req { get; set; } = DateTime.Now;

    [Required(ErrorMessage = "Keterangan wajib diisi.")]
    [MaxLength(500, ErrorMessage = "Keterangan maksimal 500 karakter.")]
    public string? Keterangan { get; set; }

    public string? FotoBukti { get; set; }

    [MaxLength(500, ErrorMessage = "Alasan maksimal 500 karakter.")]
    public string? KetChecker { get; set; }
    [MaxLength(500, ErrorMessage = "Catatan maksimal 500 karakter.")]
    public string? Feedback { get; set; }
    public DateTime? TglChecker { get; set; }
    public DateTime? TglFeedback { get; set; }
    
}

public class Pesanan
{
    [Key] public int PO_ID { get; set; }
    public string? NoPO { get; set; }
    public int PR_ID { get; set; }
    public DateTime tgl_PO { get; set; }
    public int? Status { get; set; }
    public decimal? TotalHarga { get; set; }
}

public class ChangePasswordViewModel
{
    [Required(ErrorMessage = "Password Lama wajib diisi")]
    public string PasswordLama { get; set; } = "";

    [Required(ErrorMessage = "Password Baru wajib diisi")]
    [StringLength(50, MinimumLength = 6, ErrorMessage = "Password baru minimal 6 karakter.")]
    public string PasswordBaru { get; set; } = "";

    [Required(ErrorMessage = "Konfirmasi Password wajib diisi")]
    [Compare("PasswordBaru", ErrorMessage = "Password baru dan konfirmasi tidak cocok!")]
    public string KonfirmasiPassword { get; set; } = "";
}

public class AuditLog
{
    [Key] public int Id { get; set;}

    public string? UserID { get; set; }
    public string? Username { get; set;}
    public string Action { get; set; } = "";
    public string Entity { get; set; } = "";
    public string? EntityId { get; set; }
    public string? Detail { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.Now;
    public string? IpAddress { get; set; }
    public string? Role { get; set; }
}