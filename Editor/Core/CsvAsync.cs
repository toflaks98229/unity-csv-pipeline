using System;
using System.Threading.Tasks;
using UnityEngine;

namespace CsvPipeline
{
    /// <summary>
    /// Makes the failure of a fire-and-forget task <b>reach a person</b>.
    /// <para>
    /// A sync started from the menu is <c>async void</c> in nature, so there is nowhere to await the result.
    /// Dropping it with a plain <c>_ = Work()</c> turns any exception inside into an unobserved
    /// <see cref="Task"/> exception that <b>surfaces nowhere</b>. Press Pull while the table is open in Excel
    /// and the file write fails, but what the person sees is "nothing happened". The most common way to use
    /// this is the way it fails most quietly.
    /// </para>
    /// </summary>
    public static class CsvAsync
    {
        /// <summary>
        /// Lets a task run without awaiting it, but logs it when it fails.
        /// </summary>
        /// <param name="task">Task to let run.</param>
        /// <param name="tag">Log prefix tag.</param>
        /// <param name="what">What was being done when it failed. One phrase a person reads.</param>
        public static void Forget(Task task, string tag, string what)
        {
            if (task == null) return;

            task.ContinueWith(finished =>
            {
                AggregateException error = finished.Exception;
                if (error == null) return;

                Exception cause = error.GetBaseException();
                Debug.LogError($"{tag} Error during {what}: {cause.GetType().Name} — {cause.Message}\n{cause.StackTrace}");
            }, TaskContinuationOptions.OnlyOnFaulted);
        }
    }
}
