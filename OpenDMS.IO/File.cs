
namespace OpenDMS.IO
{
    public class File : Location
    {
		#region Constructors (2) 

        public File(Directory directory, string filename)
            : base(directory.ToString() + filename)
        {
        }

        public File(string path)
            : base(System.IO.Path.GetFullPath(path))
        {
        }

		#endregion Constructors 

		#region Methods (6) 

		// Public Methods (6) 

        public string ComputeMd5()
        {
            FileStream stream;
            System.Security.Cryptography.MD5 md5;
            byte[] data;
            string output = "";

            md5 = System.Security.Cryptography.MD5.Create();

            stream = new FileStream(this, System.IO.FileMode.Open, System.IO.FileAccess.Read, System.IO.FileShare.Read, System.IO.FileOptions.None, FileSystem.Instance.BufferSize, this);

            // Compute
            data = md5.ComputeHash(stream);

            stream.Close();
            stream.Dispose();

            for (int i = 0; i < data.Length; i++)
                output += data[i].ToString("x2");

            return output;
        }

        public void Copy(File file)
        {
            System.IO.File.Copy(_path, file.ToString());
        }

        public override void Delete()
        {
            System.IO.File.Delete(_path);
        }

        public override bool Exists()
        {
            return System.IO.File.Exists(_path);
        }

        public FileStream GetStream(System.IO.FileMode mode, System.IO.FileAccess access, System.IO.FileShare share, System.IO.FileOptions options, int bufferSize, object creator)
        {
            return new FileStream(this, mode, access, share, options, bufferSize, creator);
        }

        public bool VerifyMd5(string md5ToCompare)
        {
            System.StringComparer comparer = System.StringComparer.OrdinalIgnoreCase;

            if (comparer.Compare(ComputeMd5(), md5ToCompare) != 0)
                return false;

            return true;
        }

		#endregion Methods 
    }
}
