using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Xml;

public class WeatherStats {
	public static TextWriter logFile;
	
	private static EncoderParameters encoderParameters;
	private static ImageCodecInfo codec;
	private static Picture background;
	private static Picture mask;

	private static int analyzedCount = 0;
	private static int width = 0, height = 0;
	private static int unparsedNow;

	private static int[] green = null;
	private static int[] yellow = null;
	private static int[] red = null;
	private static int[] wind = null;
	private static int[] unparsed = null;
	private static int[] overall = null;

	
	
	private static void createArrays() {
		int size = width * height;
		logFile.WriteLine("*** IMAGE SIZE: {0}x{1}", width, height);
		green = new int[size];
		yellow = new int[size];
		red = new int[size];
		wind = new int[size];
		unparsed = new int[size];
		overall = new int[size];
	}
	
	private static bool xxx = false;
	
	private static void analyzePixelNoMask(int bgr, int ofs) {
		int b = (bgr >> 16) & 0xFF;
		int g = (bgr >> 8) & 0xFF;
		int r = bgr & 0xFF;
		
		if ((Math.Abs(r - g) <= 16) && (Math.Abs(r - b) <= 16) && (Math.Abs(g - b) <= 16)) {
			// Grayscale is background (land) - skip it.
			if (xxx) logFile.WriteLine("GRAY");
			return;
		}
		if ((g > 248) && (b > 248) && (r > 200)) {
			// Light blue is background (sea) - skip it.
			return;
		}
		if (
			((b - g > 20) && (b - r > 32)) || // Dark blue - wind zone.
			((r < 16) && (g < 64) && (b > 100)) // Dark blue BORDER - wind zone.
		) {
			wind[ofs]++;
			return;
		}
		if (r - b > 64) {
			if ((r - g > 64)) {
				// Red area.
				red[ofs]++;
				return;
			} else {
				// Yellow area.
				yellow[ofs]++;
				return;
			}
		}
		if (((g - b) > 8) && ((g - r) > 24)) {
			// Green area.
			green[ofs]++;
			return;
		}
		// Otherwise it is some error or mixed colors on borders.
		unparsed[ofs]++;
		unparsedNow++;
		//logFile.WriteLine("Unparsed: {0}, {1}, {2} at {3} ({7}, {8}) - r-b {4}, r-g {5}, g-b {6}", r, g, b, ofs, r-b, r-g, g-b, ofs % width, ofs / width);
	}
	
	private static void analyzePixelWithMask(int bgr, int ofs) {
		int b = (bgr >> 16) & 0xFF;
		int g = (bgr >> 8) & 0xFF;
		int r = bgr & 0xFF;
		
		if ((Math.Abs(r - g) <= 16) && (Math.Abs(r - b) <= 16) && (Math.Abs(g - b) <= 16)) {
			// Grayscale is background (land) - skip it.
			if (xxx) logFile.WriteLine("GRAY");
			return;
		}
		if ((b - g > 50) && (b - r > 50)) {
			wind[ofs]++;
			return;
		}
		if (r - b > 80) {
			if ((r - g > 80)) {
				// Red area.
				red[ofs]++;
				return;
			} else {
				// Yellow area.
				yellow[ofs]++;
				return;
			}
		}
		if (((g - b) > 32) && ((g - r) > 32)) {
			// Green area.
			green[ofs]++;
			return;
		}
		// Otherwise it is some error or mixed colors on borders.
		unparsed[ofs]++;
		unparsedNow++;
		//logFile.WriteLine("Unparsed: {0}, {1}, {2} at {3} ({7}, {8}) - r-b {4}, r-g {5}, g-b {6}", r, g, b, ofs, r-b, r-g, g-b, ofs % width, ofs / width);
	}
	
	
	private static void analyzeImageData(Picture pic) {
		int w = pic.width;
		if (w > width) w = width;
		
		int h = pic.height;
		if (h > height) h = height;
		
		for (int y = 0; y < h; y++) {
			for (int x = 0; x < w; x++) {
				int dataOffset = y * width + x;
				if (mask != null) {
					analyzePixelWithMask(pic.getBGR(x, y), dataOffset);
				} else {
					analyzePixelNoMask(pic.getBGR(x, y), dataOffset);
				}
			}
		}
	}
	
	
	private static void analyzeImage(string imagePath) {
		Picture pic = new Picture(imagePath);
		if (mask != null) {
			pic.blendAdd(mask);
		}
		pic.saturate(2.0);
		if (Options.getBool("p")) {
			string prepDir = Options.get("prepdir");
			if (prepDir == null) {
				prepDir = Options.get("outdir");
			}
			string prepPath = replacePath(imagePath, prepDir);
			createPathForFile(prepPath);
			pic.save(codec, encoderParameters, prepPath);
		}
		
		if (width == 0) {
			width = pic.width;
			height = pic.height;
			createArrays();
		}
		
		unparsedNow = 0;
		analyzeImageData(pic);
		logFile.WriteLine("Unparsed: {0}%", (int)(unparsedNow * 10000f / width / height) / 100f);
	}
	
	
	private static string replacePath(string pathName, string newPath) {
		int lastSlash = pathName.LastIndexOf("\\");
		lastSlash = (lastSlash >= 0) ? (lastSlash + 1) : 0;
		if (!newPath.EndsWith("\\")) {
			newPath += '\\';
		}
		return newPath + pathName.Substring(lastSlash);
	}
	
	
	private static void analyzeImagesByMask(string imagesDir, string mask) {
		var images = Directory.EnumerateFiles(imagesDir, mask);
		foreach (string imagePath in images) {
			analyzeImage(imagePath);
			analyzedCount++;
		}
	}
	
	
	private static void analyzeImages(string imagesDir) {
		logFile.WriteLine("*** ANALYZING IMAGES...");
		analyzeImagesByMask(imagesDir, "*.jpg");
		analyzeImagesByMask(imagesDir, "*.jpeg");
		analyzeImagesByMask(imagesDir, "*.png");
		logFile.WriteLine("*** TOTAL FILES ANALYZED: {0}", analyzedCount);
	}
	
	
	private static void analyzeFinal() {
		for (int i = 0; i < overall.Length; i++) {
			overall[i] = red[i] * 4 + yellow[i] * 2 + green[i];
		}
	}
	
	
	delegate void Convert(int x, int y, out int bgr);
	
	
	private static void prepareResult(Picture pic, Convert convert) {
		int ofs = 0;
		int ofsY = 0;
		int bgr;
		bool hasAlpha = (pic.bytesPerPixel == 4);
		
		for (int y = 0; y < height; y++) {
			for (int x = 0; x < width; x++) {
				convert(x, y, out bgr);
				pic.bgr[ofs++] = (byte)((bgr >> 16) & 0xFF);
				pic.bgr[ofs++] = (byte)((bgr >> 8) & 0xFF);
				pic.bgr[ofs++] = (byte)(bgr & 0xFF);
				if (hasAlpha) pic.bgr[ofs++] = 0; // Unused alpha.
			}
			ofsY += pic.stride;
			ofs = ofsY;
		}
	}
	
	
	private static void mapColor(double v, int bgrMin, int bgrMax, out int bgr) {
		//logFile.WriteLine("..... Maping {0} between BGR ({1:X6}...{2:X6})", v, bgrMin, bgrMax);
		int bmin = ((bgrMin >> 16) & 0xFF);
		int gmin = ((bgrMin >> 8) & 0xFF);
		int rmin = (bgrMin & 0xFF);
		//logFile.WriteLine("..... BGR min = ({0}, {1}, {2})", bmin, gmin, rmin);
		int b = ((bgrMax >> 16) & 0xFF) - bmin;
		int g = ((bgrMax >> 8) & 0xFF) - gmin;
		int r = (bgrMax & 0xFF) - rmin;
		//logFile.WriteLine("..... BGR max-min = ({0}, {1}, {2})", b, g, r);
		//logFile.WriteLine("..... BGR multiplied = ({0}, {1}, {2}) + min = ({3}, {4}, {5})", b * v, g * v, r * v, b * v + bmin, g * v + gmin, r * v + rmin);
		b = ((int)(b * v) + bmin) & 0xFF;
		g = ((int)(g * v) + gmin) & 0xFF;
		r = ((int)(r * v) + rmin) & 0xFF;
		//logFile.WriteLine("..... BGR multiplied = ({0}, {1}, {2})", b, g, r);
		bgr = (b << 16) | (g << 8) | r;
	}
	
	
	private struct ColorMap {
		public readonly double val;
		public readonly int bgr;
		public ColorMap(double val, int bgr) { this.val = val; this.bgr = bgr; }
	}
	private static ColorMap[] mainColors = {
		new ColorMap( 0.00f, 0xFFFFFF ), // White.
		new ColorMap( 0.25f, 0x00FF00 ), // Green.
		new ColorMap( 0.50f, 0x00FFFF ), // Yellow.
		new ColorMap( 0.75f, 0x0000FF ), // Red.
		new ColorMap( 1.00f, 0xFF00FF ), // Magenta.
	};
	
	
	private static void mapFrequencyColors(double average, ColorMap[] colors, out int bgr) {
		double lowVal = colors[0].val;
		int minColor = colors[0].bgr;
		foreach (ColorMap map in colors) {
			if (average == map.val) {
				bgr = map.bgr;
				//logFile.WriteLine("avg {0} exact -> {1:X6}", average, bgr);
				return;
			}
			if (average < map.val) {
				double correctedAverage = (average - lowVal) / (map.val - lowVal);
				mapColor(correctedAverage, minColor, map.bgr, out bgr);
				//logFile.WriteLine("avg {0} < {1} -> {2} -> {3:X6}", average, map.val, correctedAverage, bgr);
				return;
			} else {
				lowVal = map.val;
				minColor = map.bgr;
			}
		}
		bgr = colors[colors.Length - 1].bgr;
		//logFile.WriteLine("avg {0} exceed -> {1:X6}", average, bgr);
	}
	
	
	private static void outputResult(string outName, bool needBg, Convert convert) {
		Picture pic = new Picture(width, height, 3);
		prepareResult(pic, convert);
		if (needBg && (background != null)) {
			pic.blendMultiply(background);
		}
		pic.save(codec, encoderParameters, outName);
	}
	
	
	private static double calcAutoMult(int[] data, string dbgName) {
		double maxAvg = 0;
		int xpos = 0, ypos = 0;
		for (int y = 0; y < height; y++) {
			for (int x = 0; x < width; x++) {
				double average = data[y * width + x] / 4f / analyzedCount;
				if (average > maxAvg) {
					maxAvg = average;
					xpos = x;
					ypos = y;
				}
			}
		}
		double mult = 1.0 / maxAvg;
		logFile.WriteLine("For {0}, calculated maxAvg = {1} --> mult = {2} at point ({3}, {4})", dbgName, maxAvg, mult, xpos, ypos);
		return mult;
	}
	
	
	private static void outputResults(string outDir, string ext) {
		if ((width <= 0) || (height <= 0)) {
			return; // No images was parsed. Size unknown.
		}
		if (!outDir.EndsWith("\\")) {
			outDir += "\\";
		}
		System.IO.Directory.CreateDirectory(outDir);
		double maxAvg, mult;
		
		// ======== OVERALL ========
		
		mult = Options.getDouble("mult");
		if (mult <= 0) {
			mult = calcAutoMult(overall, "overall");
		}

		logFile.WriteLine("Generating image: overall");
		maxAvg = 0;
		outputResult(outDir + "overall" + ext, true, (int x, int y, out int bgr) => {
			double average = overall[y * width + x] * mult / 4f / analyzedCount;
			if (average > maxAvg) maxAvg = average;
			mapFrequencyColors(average, mainColors, out bgr);
		});
		logFile.WriteLine("Average max: {0}", maxAvg);

		// ======== WIND ========

		mult = Options.getDouble("windmult");
		if (mult <= 0) {
			mult = calcAutoMult(wind, "wind");
		}

		logFile.WriteLine("Generating image: wind");
		maxAvg = 0;
		outputResult(outDir + "wind" + ext, true, (int x, int y, out int bgr) => {
			double average = wind[y * width + x] * mult  / 4f / analyzedCount;
			if (average > maxAvg) maxAvg = average;
			mapFrequencyColors(average, mainColors, out bgr);
		});
		logFile.WriteLine("Average max: {0}", maxAvg);

		// ======== UNPARSED ========

		logFile.WriteLine("Generating image: unparsed");
		int unparsedCount = 0;
		outputResult(outDir + "unparsed" + ext, false, (int x, int y, out int bgr) => {
			int v = unparsed[y * width + x];
			bgr = (v > 0) ? 0xFFFFFF : 0x000000; // White at unparsed, black others.
			if (v > 0) unparsedCount++;
		});
		logFile.WriteLine("Total Unparsed: {0}%", (int)(unparsedCount * 10000f / width / height / analyzedCount) / 100f);
	}
	
	
	/// Prepare output image codec.
	private static void prepareEncoder() {
		string mime = "image/jpeg";
		long quality = Options.getInt("quality");
		
		ImageCodecInfo[] encoders = ImageCodecInfo.GetImageEncoders();
		foreach (ImageCodecInfo imageCodec in encoders) {
			if (imageCodec.MimeType == mime) {
				codec = imageCodec;
				encoderParameters = new EncoderParameters(1);
				encoderParameters.Param[0] = new EncoderParameter(Encoder.Quality, quality);
				break;
			}
        }
	}
	
	
	public static void createPathForFile(string filePath) {
		int lastSlash = filePath.LastIndexOf("\\");
		if (lastSlash >= 0) {
			string imageDir = filePath.Substring(0, lastSlash);
			System.IO.Directory.CreateDirectory(imageDir);
		}

	}
	
	
	public static void runProcessing() {
		string bgName = Options.get("bg");
		if (bgName != null) {
			background = new Picture(bgName);
		}
		string maskName = Options.get("mask");
		if (maskName != null) {
			mask = new Picture(maskName);
		}
		
		prepareEncoder();

		analyzeImages(Options.get("imgdir"));
		if (analyzedCount > 0) {
			analyzeFinal();
			outputResults(Options.get("outdir"), ".jpg");
		} else {
			logFile.WriteLine("No images found at path \"{0}\"", Options.get("imgdir"));
		}
	}
	
	
	public static void testColorMapper() {
		logFile.WriteLine("*** TEST: testColorMapper");
		int bgr;
		for (int k = 0; k < 110; k++) {
			double average = k / 100.0;
			for (int i = 0; i < mainColors.Length; i++) {
				if (Math.Abs(mainColors[i].val - average) < 0.0001) {
					logFile.WriteLine("+++++ BEGIN LINE {0}: {1:N2} -> {2:X6}", i, mainColors[i].val, mainColors[i].bgr);
				}
			}
			mapFrequencyColors(average, mainColors, out bgr);
			logFile.WriteLine("avg = {0:N2} --> bgr = {1:X6}", average, bgr);
		}
	}
	
	
	public static void runTests() {
		logFile.WriteLine("================= TEST MODE =================");
		testColorMapper();
	}
	
	
	public static void Main(string[] args)
	{
		if (!Options.parse(args)) {
			return;
		}
		
		string logName = Options.get("log");
		if (logName != null) {
			createPathForFile(logName);
			logFile = new StreamWriter(logName, false, System.Text.Encoding.UTF8);
		} else {
			logFile = Console.Out;
		}
		
		if (Options.getBool("t") == true) {
			runTests();
		} else {
			runProcessing();
		}

		logFile.Flush();
		logFile.Close();
	}
}
