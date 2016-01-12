using System;
using System.IO;
using System.Text;
using System.Threading;

internal class Logger : IDisposable
{
	private enum LoggingMethod
	{
		Immediate,
		Retained,

		/*
		- Immediate
		Every message will be written to the log file right away in real time.
		
		- Retained
		All the messages will be retained in a temporary storage and flushed to disk
		only when the Logger object is disposed. This solves the log file sharing problem
		when Unity launched two compilation processes simultaneously, that can happen and
		happens in case of Assembly-CSharp.dll and Assembly-CSharp-Editor-firstpass.dll
		as they do not reference one another.
		*/
	}

	private const string LOG_FILENAME = "./Temp/UniversalCompiler.log";
	private const int MAXIMUM_FILE_AGE_IN_MINUTES = 5;

	private readonly Mutex mutex;
	private readonly LoggingMethod loggingMethod;
	private readonly StringBuilder pendingLines;

	public Logger()
	{
		mutex = new Mutex(true, "smcs");

		if (mutex.WaitOne(0)) // check if no other process is owning the mutex
		{
			loggingMethod = LoggingMethod.Immediate;
			DeleteLogFileIfTooOld();
		}
		else
		{
			pendingLines = new StringBuilder();
			loggingMethod = LoggingMethod.Retained;
		}
	}

	public void Dispose()
	{
		mutex.WaitOne(); // make sure we own the mutex now, so no other process is writing to the file

		if (loggingMethod == LoggingMethod.Retained)
		{
			DeleteLogFileIfTooOld();
			File.AppendAllText(LOG_FILENAME, pendingLines.ToString());
		}

		mutex.ReleaseMutex();
	}

	private void DeleteLogFileIfTooOld()
	{
		var lastWriteTime = new FileInfo(LOG_FILENAME).LastWriteTimeUtc;
		if (DateTime.UtcNow - lastWriteTime > TimeSpan.FromMinutes(MAXIMUM_FILE_AGE_IN_MINUTES))
		{
			File.Delete(LOG_FILENAME);
		}
	}

	public void AppendHeader()
	{
		var dateTimeString = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
		var middleLine = "*" + new string(' ', 78) + "*";
		int index = (80 - dateTimeString.Length) / 2;
		middleLine = middleLine.Remove(index, dateTimeString.Length).Insert(index, dateTimeString);

		Append(new string('*', 80));
		Append(middleLine);
		Append(new string('*', 80));
	}

	public void Append(string message)
	{
		if (loggingMethod == LoggingMethod.Immediate)
		{
			File.AppendAllText(LOG_FILENAME, message + Environment.NewLine);
		}
		else
		{
			pendingLines.AppendLine(message);
		}
	}
}