using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;

public class Picture {
	public readonly byte[] bgr;
	public readonly int width, height, stride, bytesPerPixel;
	private Bitmap bmp;
	private PixelFormat pixelFormat;
	
	public Picture(string imagePath) {
		this.bmp = new Bitmap(imagePath);
		this.width = bmp.Width;
		this.height = bmp.Height;
		this.pixelFormat = bmp.PixelFormat;

		switch (pixelFormat) {
			case PixelFormat.Canonical:
			case PixelFormat.Format32bppArgb:
			case PixelFormat.Format32bppRgb:
				bytesPerPixel = 4;
				break;
			case PixelFormat.Format24bppRgb:
				bytesPerPixel = 3;
				break;
			default:
				throw new Exception("Unsupported pixel format: " + pixelFormat + " - " + imagePath);
		}
		
		BitmapData bmpData = bmp.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.ReadOnly, pixelFormat);
		stride = bmpData.Stride;
		if (stride < 0) {
			throw new Exception("Unsupported bottom-top images (stride < 0)" + " - " + imagePath);
		}
		int byteSize = Math.Abs(stride) * height;
		bgr = new byte[byteSize]; // Declare an array to hold the bytes of the bitmap.

		WeatherStats.logFile.WriteLine("Bitmap \"{0}\" format: {1} {2}x{3} stride {4}", imagePath, pixelFormat, width, height, stride);

		// Copy the BGR values into the array.
		System.Runtime.InteropServices.Marshal.Copy(bmpData.Scan0, bgr, 0, bgr.Length);
		bmp.UnlockBits(bmpData);
	}
	
	public Picture(int w, int h, int bytesPerPix) {
		this.width = w;
		this.height = h;
		this.bytesPerPixel = bytesPerPix;

		switch(bytesPerPixel) {
			case 3:
				pixelFormat = PixelFormat.Format24bppRgb;
				break;
			case 4:
				pixelFormat = PixelFormat.Format32bppArgb;
				break;
			default:
				throw new Exception("Unsupported bytes per pixel: " + bytesPerPixel);
		}

		bmp = new Bitmap(width, height, pixelFormat);
		
		BitmapData bmpData = bmp.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.WriteOnly, pixelFormat);
		stride = bmpData.Stride;
		if (stride < 0) {
			throw new Exception("Unsupported bottom-top images (stride < 0)");
		}
		int byteSize = Math.Abs(stride) * height;
		bgr = new byte[byteSize]; // Declare an array to hold the bytes of the bitmap.

		bmp.UnlockBits(bmpData);
	}
	
	public void save(ImageCodecInfo codec, EncoderParameters encoderParameters, string imagePath) {
		BitmapData bmpData = bmp.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.WriteOnly, pixelFormat);

		// Copy the BGR values back to the bitmap.
		System.Runtime.InteropServices.Marshal.Copy(bgr, 0, bmpData.Scan0, bgr.Length);
		
		bmp.UnlockBits(bmpData);
		
		bmp.Save(imagePath, codec, encoderParameters);
	}
	
	public int getBGR(int x, int y) {
		int ofs = (y * stride) + (x * bytesPerPixel);
		return (bgr[ofs] << 16) | (bgr[ofs + 1] << 8) | bgr[ofs + 2];
	}
}
