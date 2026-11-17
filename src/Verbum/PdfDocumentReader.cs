using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Verbum;

public static class PdfDocumentReader
{
    private static readonly object Sync = new();
    static PdfDocumentReader()
    {
        NativeLibrary.SetDllImportResolver(typeof(PdfDocumentReader).Assembly, (name, _, _) => name == "verbum_pdfium" ? NativeLibrary.Load(Path.Combine(AppContext.BaseDirectory, "native", "pdfium.dll")) : IntPtr.Zero);
        Init();
    }
    [DllImport("verbum_pdfium", EntryPoint = "FPDF_InitLibrary")] private static extern void Init();
    [DllImport("verbum_pdfium", EntryPoint = "FPDF_LoadDocument")] private static extern IntPtr Open([MarshalAs(UnmanagedType.LPUTF8Str)] string file, IntPtr password);
    [DllImport("verbum_pdfium", EntryPoint = "FPDF_CloseDocument")] private static extern void Close(IntPtr document);
    [DllImport("verbum_pdfium", EntryPoint = "FPDF_GetPageCount")] private static extern int Count(IntPtr document);
    [DllImport("verbum_pdfium", EntryPoint = "FPDF_LoadPage")] private static extern IntPtr Page(IntPtr document, int index);
    [DllImport("verbum_pdfium", EntryPoint = "FPDF_ClosePage")] private static extern void ClosePage(IntPtr page);
    [DllImport("verbum_pdfium", EntryPoint = "FPDF_GetPageWidth")] private static extern double Width(IntPtr page);
    [DllImport("verbum_pdfium", EntryPoint = "FPDF_GetPageHeight")] private static extern double Height(IntPtr page);
    [DllImport("verbum_pdfium", EntryPoint = "FPDFText_LoadPage")] private static extern IntPtr TextPage(IntPtr page);
    [DllImport("verbum_pdfium", EntryPoint = "FPDFText_ClosePage")] private static extern void CloseText(IntPtr text);
    [DllImport("verbum_pdfium", EntryPoint = "FPDFText_CountChars")] private static extern int Chars(IntPtr text);
    [DllImport("verbum_pdfium", EntryPoint = "FPDFText_GetText")] private static extern int Text(IntPtr text, int start, int count, [Out] ushort[] buffer);
    [DllImport("verbum_pdfium", EntryPoint = "FPDFBitmap_Create")] private static extern IntPtr Bitmap(int width, int height, int alpha);
    [DllImport("verbum_pdfium", EntryPoint = "FPDFBitmap_Destroy")] private static extern void DestroyBitmap(IntPtr bitmap);
    [DllImport("verbum_pdfium", EntryPoint = "FPDFBitmap_FillRect")] private static extern void Fill(IntPtr bitmap, int left, int top, int width, int height, uint color);
    [DllImport("verbum_pdfium", EntryPoint = "FPDF_RenderPageBitmap")] private static extern void Render(IntPtr bitmap, IntPtr page, int x, int y, int width, int height, int rotation, int flags);
    [DllImport("verbum_pdfium", EntryPoint = "FPDFBitmap_GetBuffer")] private static extern IntPtr Buffer(IntPtr bitmap);
    [DllImport("verbum_pdfium", EntryPoint = "FPDFBitmap_GetStride")] private static extern int Stride(IntPtr bitmap);
    public static List<DocumentSection> Extract(string path)
    {
        lock (Sync)
        {
            IntPtr doc = Open(path, IntPtr.Zero); if (doc == IntPtr.Zero) throw new InvalidDataException("PDF ilegível, protegido por senha ou não suportado.");
            try
            {
                int count = Count(doc); if (count is < 1 or > 20000) throw new InvalidDataException("Número de páginas não suportado.");
                var sections = new List<DocumentSection>();
                for (int i = 0; i < count; i++)
                {
                    IntPtr page = Page(doc, i); if (page == IntPtr.Zero) throw new InvalidDataException($"Não foi possível ler a página {i + 1}.");
                    try
                    {
                        IntPtr text = TextPage(page); string value = "";
                        if (text != IntPtr.Zero) try { int n = Chars(text); if (n > 0 && n < 5_000_000) { var buffer = new ushort[n + 1]; int read = Text(text, 0, n, buffer); value = new string(buffer.Take(Math.Max(0, read - 1)).Select(c => (char)c).ToArray()); } } finally { CloseText(text); }
                        sections.Add(new() { Title = $"Página {i + 1}", Blocks = [new() { Text = value.Replace("\0", "") }] });
                    }
                    finally { ClosePage(page); }
                }
                return sections;
            }
            finally { Close(doc); }
        }
    }
    public static BitmapSource RenderPage(string path, int index, int pixels)
    {
        lock (Sync)
        {
            IntPtr doc = Open(path, IntPtr.Zero); if (doc == IntPtr.Zero) throw new InvalidDataException("Não foi possível abrir o PDF.");
            try
            {
                IntPtr page = Page(doc, Math.Clamp(index, 0, Count(doc) - 1)); if (page == IntPtr.Zero) throw new InvalidDataException("Página indisponível.");
                try
                {
                    int w = Math.Clamp(pixels, 300, 2400), h = Math.Clamp((int)(w * Height(page) / Width(page)), 100, 6000);
                    IntPtr bitmap = Bitmap(w, h, 1); if (bitmap == IntPtr.Zero) throw new InvalidDataException("Memória insuficiente para renderizar a página.");
                    try { Fill(bitmap, 0, 0, w, h, 0xffffffff); Render(bitmap, page, 0, 0, w, h, 0, 1); int stride = Stride(bitmap); var bytes = new byte[stride * h]; Marshal.Copy(Buffer(bitmap), bytes, 0, bytes.Length); var result = BitmapSource.Create(w, h, 96, 96, PixelFormats.Bgra32, null, bytes, stride); result.Freeze(); return result; }
                    finally { DestroyBitmap(bitmap); }
                }
                finally { ClosePage(page); }
            }
            finally { Close(doc); }
        }
    }
}
