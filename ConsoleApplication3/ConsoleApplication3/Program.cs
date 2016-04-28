using System;
using System.Drawing;
using System.IO;
using System.Text;
using System.Windows.Forms;

namespace ConsoleApplication3
{
    public static class Program
    {

        [STAThread]
        static void Main(string[] args)
        {
            if (args.Length == 0)
            {
                Console.WriteLine("Error: Please use either -h to hide an image or -u to unhide an image.");
                Console.WriteLine("Running in debug mode...");
                string[] debugArgs = new string[] { "-h", @"C:\Users\misatran\OneDrive\Pictures\Engagement photos\Sharel and Michael-2.jpg" };
                Run(debugArgs);
            }
            else
            {
                Run(args);
            }
        }

        [STAThread]
        public static void Run(string[] args)
        {
            switch (args[0])
            {
                // Hide
                case "-h":
                    var image = args[1];
                    if (args.Length <= 1)
                    {
                        Console.WriteLine("Error: Please specify the image that you want to hide data into.");
                        return;
                    }

                    Console.WriteLine($"The maximum amount of bytes you can write to {image} is {MaxBytes(image)}");
                    Console.WriteLine($"Please choose the file to hide");

                    OpenFileDialog dia = new OpenFileDialog();
                    dia.Title = $"Choose a file to hide in {image}";
                    if (dia.ShowDialog() == DialogResult.OK)
                    {
                        byte[] data = System.IO.File.ReadAllBytes(dia.FileName);
                        PutDataInImage(data, image, dia.SafeFileName, image + "_HIDDEN.png");
                    }

                    Console.WriteLine($"Successfully hid {dia.FileName} in {image}");
                    break;
                case "-u":
                    if (args.Length <= 1)
                    {
                        Console.WriteLine("Error: Please specify the image that you want to extract hidden data from.");
                        return;
                    }
                    var file = GetMessageInImage(args[1]);

                    Console.WriteLine($"Found {file}... Extracting it from {args[1]}...");
                    byte[] result = GetDataInImage(args[1]);
                    Console.WriteLine($"Done...Writing to {args[1] + file}");
                    System.IO.File.WriteAllBytes(args[1] + file, result);
                    Console.WriteLine($"Successfully saved to {args[1] + file}");
                    break;

            }
        }

        public static void PutDataInImage(byte[] data, string location, string message, string hiddenImageName)
        {
            using (var bmp = new Bitmap($"{location}"))
            {
                var pxHeight = bmp.Height;
                var pxWidth = bmp.Width;

                // This is the header, and consists of the meta data to store.
                byte[] dataSize = BitConverter.GetBytes(data.Length);
                byte[] name = System.Text.ASCIIEncoding.ASCII.GetBytes(message);
                byte[] nameSize = BitConverter.GetBytes(name.Length);
                byte[] header = new byte[dataSize.Length + nameSize.Length + name.Length];

                // Combine all the header info into a header byte array.
                dataSize.CopyTo(header, 0);
                nameSize.CopyTo(header, dataSize.Length);
                name.CopyTo(header, dataSize.Length + nameSize.Length);

                //Header format:
                // | buffSize | messageSize | message | rawData |
                // | int32    | int32       | char[]  | byte[]  |

                int max = MaxBytes(location);

                if (data.Length > max)
                {
                    throw new InvalidOperationException($"The data length cannot be more than {max}");
                }

                var transform = new byte[data.Length + (header.Length * 2)];
                header.CopyTo(transform, 0);
                data.CopyTo(transform, header.Length);

                int i = 0;
                // Iterate through the pixels to insert the header then the data then the header again.
                for (; i < transform.Length * 8; i++)
                {
                    // Use integer math to iterate through the pixels.
                    int x = i % pxWidth;
                    int y = i / pxWidth;

                    var pixel = bmp.GetPixel(x, y);
                    // Calaculate offset least significant bit.
                    var offbit = transform[i / 8].GetBitAtIndex(i % 8);
                    // Or the lsb into each ARGB value for (x, y).
                    int r = (pixel.R & 254) | offbit;
                    int g = (pixel.G & 254) | offbit;
                    int b = (pixel.B & 254) | offbit;
                    // Set the least significant bits.
                    bmp.SetPixel(x, y, Color.FromArgb(255, r, g, b));
#if DEBUG_ON
                    var test = bmp.GetPixel(x, y);
                    var lsbFromPixel = (bmp.GetPixel(x, y).R & 0x1) & (bmp.GetPixel(x, y).G & 0x1) & (bmp.GetPixel(x, y).B & 0x1);
                    Console.Write((i > 0 && i % 4 == 0 ? " " : "") + (i > 0 && i % 8 == 0 ? $" at ({x}, {y})\n" : "") + lsbFromPixel);
#endif
                }
                // Save the new image.
                bmp.Save(hiddenImageName, System.Drawing.Imaging.ImageFormat.Png);

                Console.WriteLine($"Modified file save at {hiddenImageName}");

            }
        }

        /// <summary>
        /// The maximum amount of bytes you can write in the location file.
        /// </summary>
        public static int MaxBytes(string location)
        {
            int max = 0;

            using (var bmp = new Bitmap(location))
            {
                // The maximum amount of data that can be fit into the supplied image.
                max = ((bmp.Height * bmp.Width) - 8)/8;
            }

            return max;
        }

        public static byte[] GetDataInImage(string location)
        {
            byte[] data;
            using (var bmp = new Bitmap(location))
            {
                byte[] dataSize = ReadLsbPixel(bmp, 0, 4);
                byte[] nameSize = ReadLsbPixel(bmp, dataSize.Length, 4);
                byte[] name = ReadLsbPixel(bmp, dataSize.Length + nameSize.Length, BitConverter.ToInt32(nameSize, 0));
                data = ReadLsbPixel(bmp, dataSize.Length + nameSize.Length + name.Length, BitConverter.ToInt32(dataSize, 0));
            }

            return data;
        }

        public static string GetMessageInImage(string location)
        {
            byte[] name;
            using (var bmp = new Bitmap(location))
            {
                byte[] dataSize = ReadLsbPixel(bmp, 0, 4);
                byte[] nameSize = ReadLsbPixel(bmp, dataSize.Length, 4);
                name = ReadLsbPixel(bmp, dataSize.Length + nameSize.Length, BitConverter.ToInt32(nameSize, 0));
            }

            return System.Text.ASCIIEncoding.ASCII.GetString(name);
        }

        public static string ToBinaryString(this byte val)
        {
            StringBuilder build = new StringBuilder();
            for (int i = 0; i < 8; i++)
            {
                build.Insert(build.Length, i == 4 ? $" {val.GetBitAtIndex(i)}" : $"{val.GetBitAtIndex(i)}");
            }
            return build.ToString();
        }

        /// <summary>
        /// Returns 0 or 1 depending on what bit is at the 0-indexed, pos.
        /// </summary>
        public static int GetBitAtIndex(this byte val, int pos) => ((val << pos) & 128) / 128;

        private static byte[] ReadLsbPixel(Bitmap bmp, int startPos, int length)
        {
            byte[] data = new byte[length];

            for (int i = 0; i < data.Length * 8; i++)
            {
                int x = (i+(startPos*8)) % bmp.Width;
                int y = (i+(startPos*8)) / bmp.Width;

                var pixel = bmp.GetPixel(x, y);
                int olsb = (pixel.R & 0x1) & (pixel.G & 0x1) & (pixel.B & 0x1);
#if DEBUG_ON
                    Console.Write((i > 0 && i % 4 == 0 ? " " : "") + (i > 0 && i % 8 == 0 ? $" at ({x}, {y})\n" : "") + olsb);
#endif
                data[i / 8] <<= 1;
                data[i / 8] |= (byte)(olsb);
            }

            return data;
        }
    }
}
