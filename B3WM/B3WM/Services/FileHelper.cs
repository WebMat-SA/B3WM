using System.Text;

namespace B3WM.Services
{
    public static class FileHelper
    {
        public static IEnumerable<string> ReadLinesReverse(string path)
        {
            using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (var reader = new BinaryReader(fs, Encoding.UTF8))
            {
                long position = fs.Length - 1;
                var sb = new StringBuilder();

                while (position >= 0)
                {
                    fs.Seek(position, SeekOrigin.Begin);
                    char c = reader.ReadChar();

                    if (c == '\n')
                    {
                        if (sb.Length > 0)
                        {
                            yield return ReverseString(sb.ToString());
                            sb.Clear();
                        }
                    }
                    else if (c != '\r') // ignora CR
                    {
                        sb.Append(c);
                    }

                    position--;
                }

                // primeira linha do arquivo
                if (sb.Length > 0)
                    yield return ReverseString(sb.ToString());
            }
        }

        private static string ReverseString(string s)
        {
            char[] arr = s.ToCharArray();
            Array.Reverse(arr);
            return new string(arr);
        }
    }
}
