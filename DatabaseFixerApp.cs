using EVCharging.BE.DAL;

namespace DatabaseFixerApp
{
    class Program
    {
        static async Task Main(string[] args)
        {
            try
            {
                Console.WriteLine("🔧 Bắt đầu fix database...");
                await DatabaseFixer.FixUserPasswordsAsync();
                Console.WriteLine("✅ Hoàn thành! Bây giờ bạn có thể đăng nhập với:");
                Console.WriteLine("   Email: chinh22@gmail.com");
                Console.WriteLine("   Password: 12345");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Lỗi: {ex.Message}");
            }
            
            Console.WriteLine("\nNhấn Enter để thoát...");
            Console.ReadLine();
        }
    }
}
