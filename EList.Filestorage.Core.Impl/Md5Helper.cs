using System.Security.Cryptography;

namespace EList.Filestorage.Core.Impl
{
    public static class Md5Helper
    {
        public static string GetHash(Stream stream)
        {
            var md5 = CalculateMD5Async(stream).GetAwaiter().GetResult();
            var hash = Convert.ToBase64String(md5);
            return hash;
        }

        static async Task<byte[]> CalculateMD5Async(Stream stream)
        {
            using var md5 = MD5.Create();

            byte[] buffer = new byte[4096];
            int bytesRead;
            do
            {
                bytesRead = await stream.ReadAsync(buffer, 0, 4096);
                if (bytesRead > 0)
                {
                    md5.TransformBlock(buffer, 0, bytesRead, null, 0);
                }
            } while (bytesRead > 0);

            md5.TransformFinalBlock(buffer, 0, 0);
            return md5.Hash;
        }
    }
}