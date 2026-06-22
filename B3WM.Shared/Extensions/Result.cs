using System;
using System.Collections.Generic;
using System.Text;

namespace B3WM.Shared.Extensions
{
    public class Result<T>
    {
        public bool IsValid { get; set; } = false;
        public T Data { get; set; }
        public bool Unlogged { get; set; } = false;

        public Result(bool _isValid,T _data, bool _unlogged = false)
        {
            IsValid = _isValid;
            Data = _data;
            Unlogged = _unlogged;
        }

        public Result() { }

    }
}
