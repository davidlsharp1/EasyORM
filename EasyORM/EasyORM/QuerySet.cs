using System;
using System.Collections.Generic;
using System.Text;

namespace EasyORM
{
    internal class QuerySet
    {
        public List<QueryParams> ParamLS { get; set; } = new List<QueryParams>();

        public void AddParameter(string strFieldName, string strOperator, string strValue)
        {
            var p = new QueryParams() { FieldName = strFieldName, Operator = strOperator, Value = strValue };
            ParamLS.Add(p);
        }
    }
}
