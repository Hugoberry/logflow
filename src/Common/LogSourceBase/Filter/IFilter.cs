﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LogFlow.DataModel
{
    public interface IFilter
    {
        string Name { get; }
        bool Match<T>(T item, string template) where T : DataItemBase;
    }
}