namespace ProFlowApp.ViewModels
{
    public class LaporanPRViewModel
    {
        // Filter
        public DateTime? TanggalDari { get; set; }
        public DateTime? TanggalSampai { get; set; }
        public int? Status { get; set; }
        public string? NamaKaryawan { get; set; }
        public string TipeGrafik { get; set; } = "tahun";

        // Summary Cards
        public int TotalPR { get; set; }
        public int TotalPending { get; set; }
        public int TotalDisetujui { get; set; }
        public int TotalDitolak { get; set; }

        // Tabel Detail
        public List<LaporanPRItem> Items { get; set; } = new();
        public int TotalJumlah { get; set; }
        public decimal GrandTotal { get; set; }

        // Grafik
        public List<GrafikItem> DataGrafik { get; set; } = new();
    }

    public class LaporanPRItem
    {
        public int No { get; set; }
        public string? NoPR { get; set; } = string.Empty;
        public DateTime TanggalPR { get; set; }
        public string? NamaKaryawan { get; set; } = string.Empty;
        public string? NamaBarang { get; set; } = string.Empty;
        public string? Satuan { get; set; } = string.Empty;
        public int Jumlah { get; set; }
        public decimal? HargaSatuan { get; set; }
        public decimal? TotalHarga { get; set; }
        public int? Status { get; set; }
        public string StatusLabel => !Status.HasValue ? "Belum Ditentukan" : Status.Value == 0 ? "Pending" : Status.Value == 1 ? "Disetujui" : "Ditolak";
    }

    public class GrafikItem
    {
        public int Tahun { get; set; }
        public int Bulan { get; set; }
        public string? NamaBulan { get; set; } = string.Empty;
        public int MingguKe { get; set; }
        public int JumlahPR { get; set; }
        public int JumlahPending { get; set; }
        public int JumlahDisetujui { get; set; }
        public int JumlahDitolak { get; set; }
        public string Label => MingguKe == 0 ? (NamaBulan ?? string.Empty) : $"Minggu Ke-{MingguKe}";
    }
}