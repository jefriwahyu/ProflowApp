namespace ProFlowApp.ViewModels
{
    public class LaporanPOViewModel
    {
        // Filter
        public DateTime? TanggalDari { get; set; }
        public DateTime? TanggalSampai { get; set; }
        public int? Status { get; set; }
        public string? NamaKaryawan { get; set; }
        public string TipeGrafik { get; set; } = "tahun";

        // Summary Cards
        public int TotalPO { get; set; }
        public int TotalPending { get; set; }
        public int TotalDiproses { get; set; }
        public int TotalSelesai { get; set; }

        // Tabel Detail
        public List<LaporanPOItem> Items { get; set; } = new();
        public decimal GrandTotal { get; set; }

        // Grafik
        public List<GrafikPOItem> DataGrafik { get; set; } = new();
    }

    public class LaporanPOItem
    {
        public int No { get; set; }
        public string? NoPO { get; set; }
        public string? NoPR { get; set; }
        public DateTime TanggalPO { get; set; }
        public string? NamaKaryawan { get; set; }
        public string? NamaBarang { get; set; }
        public string? Satuan { get; set; }
        public int Jumlah { get; set; }
        public decimal? HargaSatuan { get; set; }
        public decimal? TotalHarga { get; set; }
        public int? Status { get; set; }
        public string StatusLabel => !Status.HasValue ? "Belum Ditentukan" : Status.Value == 0 ? "Pending" : Status.Value == 1 ? "Diproses" : "Selesai";
    }

    public class GrafikPOItem
    {
        public int? Tahun { get; set; }
        public int? Bulan { get; set; }
        public string? NamaBulan { get; set; }
        public int? MingguKe { get; set; }
        public int? JumlahPO { get; set; }
        public int? JumlahPending { get; set; }
        public int? JumlahDiproses { get; set; }
        public int? JumlahSelesai { get; set; }
        public string Label => MingguKe == 0 || MingguKe == null ? (NamaBulan ?? "") : $"Minggu Ke-{MingguKe}";
    }
}
