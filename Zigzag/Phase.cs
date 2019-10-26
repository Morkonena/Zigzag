using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

/// <summary>
/// Represents one phase in compilation
/// </summary>
public abstract class Phase
{
	private List<Task<Status>> Tasks = new List<Task<Status>>();
	public bool Multithread { get; set; }

	public bool Failed => Tasks.Any(t => !t.IsCompleted || t.Result.IsProblematic);

	/// <summary>
	/// Executes the phase with the given data 
	/// </summary>
	/// <param name="bundle">Data collection that the phase may need</param>
	/// <returns>Status returned from the phase</returns>
	public abstract Status Execute(Bundle bundle);

	/// <summary>
	/// Executes runnable on another thread if multithreading is enabled, otherwise executes locally
	/// </summary>
	/// <param name="task">Task to run</param>
	public void Run(Func<Status> task)
	{
		if (Multithread)
		{
			Tasks.Add(Task.Run(task));
		}
		else
		{
			Status status;

			try
			{
				status = task();
			}
			catch (Exception e)
			{
				status = Status.Error(e.Message);
			}

			Tasks.Add(Task.FromResult(status));
		}
	}

	/// <summary>
	/// Waits for all tasks to finish
	/// </summary>
	public void Sync()
	{
		int i = 0;

		while (i < Tasks.Count)
		{
			Tasks[i++].Wait();
		}
	}


	/// <summary>
	/// Aborts the execution
	/// </summary>
	public void Abort()
	{
		Environment.Exit(1);
	}
}