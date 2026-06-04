namespace ProFlowApp.ViewModels;

public class PengajuanIndexViewModel
{
    public string NoPR { get; set; } = "";
    public string NamaBarang { get; set; } = "";
    public int Jumlah { get; set; }
    public int? Status { get; set; }

    // Update StatusLabel — dari 3 status menjadi 5 status
    public string StatusLabel => Status switch
    {
        0 => "Pending",
        1 => "Perlu Dicek",
        2 => "Sudah Dicek",
        3 => "Disetujui",
        4 => "Ditolak",
        _ => "Belum Ditentukan"
    };

    public DateTime Tanggal { get; set; }
    public string? NoPO { get; set; }

    // Data karyawan — untuk modal
    public string? Keterangan { get; set; }

    // Data checker — untuk modal
    // Nullable karena baru ada setelah checker submit
    public string? FotoBukti { get; set; }
    public string? KetChecker { get; set; }
    public DateTime? TglChecker { get; set; }

    // Data feedback manager — untuk modal
    // Nullable karena baru ada setelah manager keputusan
    public string? Feedback { get; set; }
    public DateTime? TglFeedback { get; set; }

    // Nama karyawan pembuat PR — untuk ditampilkan di modal
    public string NamaKaryawan { get; set; } = "";
    public DateTime TglPR { get; set; }
}