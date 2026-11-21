using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Library.Lab3
{
    public enum CycleMode
    {
        /// <summary>
        /// Режим не выбран
        /// </summary>
        None,
        /// <summary>
        /// Сумма префикса
        /// </summary>
        PrefixSum,
        /// <summary>
        /// Подсчёт элементов префикса, превышающих порог
        /// </summary>
        CountGreaterThanT,
        /// <summary>
        /// Максимум префикса
        /// </summary>
        PrefixMax
    }
}
