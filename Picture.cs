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


	public void setBGR(int x, int y, int bgrColor) {
		int ofs = (y * stride) + (x * bytesPerPixel);
		bgr[ofs++] = (byte)((bgrColor >> 16) & 0xFF);
		bgr[ofs++] = (byte)((bgrColor >> 8) & 0xFF);
		bgr[ofs] = (byte)(bgrColor & 0xFF);
	}


	// Implements layers blend mode: addition.
	public void blendAdd(Picture other) {
		byte[] p = this.bgr;
		byte[] m = other.bgr;
		int len = Math.Min(p.Length, m.Length);
		for (int i = 0; i < len; i++) {
			int sum = p[i] + m[i];
			p[i] = (byte)((sum > 255) ? 255 : sum);
		}
	}


	// Implements layers blend mode: subtraction.
	public void blendSubtract(Picture other) {
		byte[] p = this.bgr;
		byte[] m = other.bgr;
		int len = Math.Min(p.Length, m.Length);
		for (int i = 0; i < len; i++) {
			int sub = p[i] - m[i];
			p[i] = (byte)((sub < 0) ? 0 : sub);
		}
	}


	// Implements layers blend mode: multiplication.
	public void blendMultiply(Picture other) {
		byte[] p = this.bgr;
		byte[] m = other.bgr;
		int len = Math.Min(p.Length, m.Length);
		for (int i = 0; i < len; i++) {
			int mul = ((int)p[i] * (int)m[i]) >> 8;
			p[i] = (byte)((mul > 255) ? 255 : mul);
		}
	}


	public void saturate(double byValue) {
		byte[] p = this.bgr;
		for (int i = 0; i < p.Length; i += bytesPerPixel) {
			int b = p[i  ]; 
			int g = p[i+1];
			int r = p[i+2];
			double gray = 0.2989f*r + 0.5870f*g + 0.1140f*b; // Weights from CCIR 601 spec.
			
			gray *= byValue;
			double mult = byValue + 1;
			
			b = (int)Math.Round(b * mult - gray);
			g = (int)Math.Round(g * mult - gray);
			r = (int)Math.Round(r * mult - gray);
			
			p[i  ] = (byte)((b > 255) ? 255 : ((b < 0) ? 0 : b));
			p[i+1] = (byte)((g > 255) ? 255 : ((g < 0) ? 0 : g));
			p[i+2] = (byte)((r > 255) ? 255 : ((r < 0) ? 0 : r));
		}
	}
}
