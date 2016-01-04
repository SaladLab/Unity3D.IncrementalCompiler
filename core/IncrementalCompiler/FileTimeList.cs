using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace IncrementalCompiler
{
    public class FileTimeList
    {
        private List<Tuple<string, DateTime>> _files;

        public class Result
        {
            public IEnumerable<string> Added;
            public IEnumerable<string> Changed;
            public IEnumerable<string> Removed;
        }

        public Result Update(IEnumerable<string> files)
        {
            return Update(files.Select(file => Tuple.Create(file, File.GetLastWriteTime(file))));
        }

        public Result Update(IEnumerable<Tuple<string, DateTime>> files)
        {
            var oldFiles = _files;
            _files = files.OrderBy(file => file.Item1).ToList();

            if (oldFiles == null)
            {
                return new Result
                {
                    Added = Enumerable.Empty<string>(),
                    Changed = Enumerable.Empty<string>(),
                    Removed = Enumerable.Empty<string>(),
                };
            }

            // get differences

            var added = new List<string>();
            var changed = new List<string>();
            var removed = new List<string>();

            var i = 0;
            var j = 0;
            while (i < _files.Count && j < oldFiles.Count)
            {
                var c = Comparer<string>.Default.Compare(_files[i].Item1, oldFiles[j].Item1);
                if (c == 0)
                {
                    if (_files[i].Item2 != oldFiles[j].Item2)
                        changed.Add(_files[i].Item1);
                    i += 1;
                    j += 1;
                }
                else if (c < 0)
                {
                    added.Add(_files[i].Item1);
                    i += 1;
                }
                else
                {
                    removed.Add(oldFiles[j].Item1);
                    j += 1;
                }
            }
            for (; i < _files.Count; i++)
                added.Add(_files[i].Item1);
            for (; j < oldFiles.Count; j++)
                removed.Add(oldFiles[j].Item1);

            return new Result
            {
                Added = added,
                Changed = changed,
                Removed = removed,
            };
        }
    }
}
