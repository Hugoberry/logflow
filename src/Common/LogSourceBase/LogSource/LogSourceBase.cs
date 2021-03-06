﻿namespace LogFlow.DataModel
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Reflection;
    using System.Threading;
    using LogFlow.DataModel.Algorithm;

    public abstract class LogSourceBase<T> : ILogSource<T>, IDisposable where T : DataItemBase
    {
        // Memory optimize critical
        protected HashTablePool localHashTableStringPool = new HashTablePool();

        protected LogSourceBase(LogSourceProperties properties)
        {
            this.Properties = properties;
            this.propertyInfos = DataItemBase.GetPropertyInfos<T>();

            this.columnInfos = DataItemBase.GetColumnInfos(propertyInfos);
        }

        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected abstract void Dispose(bool isDisposing);

        public abstract string Name { get; }

        public LogSourceProperties Properties { get; }
        public virtual int Count => this.InternalItems.Count;
        public virtual int Tier1Count { get; } = 0;
        public virtual int Tier2Count { get; } = 0;

        public virtual T this[int index] => this.InternalItems[index];

        // TODO: consider identifier cache
        private readonly List<T> internalItems = new List<T>();
        protected IReadOnlyList<T> InternalItems => this.internalItems;

        private readonly List<string[]> parameters = new List<string[]>();
        protected IReadOnlyList<string[]> Parameters => this.parameters;

        protected IdentifierCache<string> files = new IdentifierCache<string>();

        public IReadOnlyList<string> Templates => this.templates;
        private readonly IdentifierCache<string> templates = new IdentifierCache<string>();

        public IReadOnlyList<PropertyInfo> PropertyInfos => this.propertyInfos;
        private readonly List<PropertyInfo> propertyInfos;

        public IReadOnlyList<ColumnInfoAttribute> ColumnInfos => this.columnInfos;
        private readonly List<ColumnInfoAttribute> columnInfos;

        public IReadOnlyList<IFilter> GroupFilters => InnerGroupFilters;
        protected List<IFilter> InnerGroupFilters = null;

        private object GetColumnHtml(DataItemBase item, int columnIndex)
        {
            ColumnInfoAttribute ci = this.ColumnInfos[columnIndex];

            if (string.Equals(ci.Name, "Text", StringComparison.Ordinal))
            {
                return ((ParametricString)this.GetColumnValue(item, columnIndex)).ToHtml();
            }
            else
            {
                return this.GetColumnValue(item, columnIndex);
            }
        }

        public string GetHtml(IEnumerable<DataItemBase> items, bool withTitle)
        {
            var result = string.Join(
                Environment.NewLine,
                items.Select(item =>
                    $"<tr>{string.Concat(Enumerable.Range(0, this.ColumnInfos.Count).Select(i => $"<td>{this.GetColumnHtml(item, i)}</td>"))}</tr>"));

            if (withTitle)
            {
                var title = $"<tr>{string.Concat(Enumerable.Range(0, this.ColumnInfos.Count).Select(i => $"<td>{this.ColumnInfos[i].Name}</td>"))}</tr>";
                return title + Environment.NewLine + result;
            }

            return result;
        }

        public string GetText(IEnumerable<DataItemBase> items, bool withTitle)
        {
            var result = string.Join(
                Environment.NewLine,
                items.Select(item =>
                    string.Join("\t", Enumerable.Range(0, this.ColumnInfos.Count).Select(i => this.GetColumnValue(item, i)))));

            if (withTitle)
            {
                var title = string.Join("\t", Enumerable.Range(0, this.ColumnInfos.Count).Select(i => this.ColumnInfos[i].Name));
                return title + Environment.NewLine + result;
            }

            return result;
        }

        public virtual object GetColumnValue(DataItemBase item, int columnIndex)
        {
            ColumnInfoAttribute ci = this.ColumnInfos[columnIndex];

            if (string.Equals(ci.Name, "Text", StringComparison.Ordinal))
            {
                return new ParametricString(
                    this.Templates[item.TemplateId],
                    item.Parameters);
            }
            else if (string.Equals(ci.Name, "Time", StringComparison.Ordinal))
            {
                return item.Time.ToString("s");
            }
            else if (string.Equals(ci.Name, "File", StringComparison.Ordinal))
            {
                return this.files[item.FileIndex];
            }
            else
            {
                PropertyInfo pi = this.PropertyInfos[columnIndex];
                return pi.GetMethod.Invoke(item, null);
            }
        }

        protected virtual void AddItem(FullDataItem<T> item)
        {
            this.localHashTableStringPool.Intern(item.Item.Parameters);
            this.internalItems.Add(item.Item);
            item.Item.Id = this.InternalItems.Count - 1;
        }

        protected int AddTemplate(string template)
        {
            return this.templates.Put(template);
        }

        protected void AddParameters(string[] values)
        {
            for (int i = 0; i < values.Length; i++) values[i] = this.localHashTableStringPool.Intern(values[i]);
            this.parameters.Add(values);
        }

        private bool firstBatchLoaded;
        protected int CurrentId = 0;

        public virtual IEnumerable<int> Peek(IFilter filter, int peekCount, CancellationToken token) { yield break; }

        public virtual IEnumerable<int> Load(IFilter filter, CancellationToken token)
        {
            if (firstBatchLoaded) return this.LoadIncremental(filter, token);

            firstBatchLoaded = true;
            return this.LoadFirst(filter, token);
        }

        protected virtual IEnumerable<int> LoadFirst(IFilter filter, CancellationToken token) { yield break; }
        protected virtual IEnumerable<int> LoadIncremental(IFilter filter, CancellationToken token) { yield break; }
    }
}
