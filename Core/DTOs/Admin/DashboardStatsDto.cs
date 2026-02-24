using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Core.DTOs.Admin
{
    public class DashboardStatsDto
    {
        public int TotalOrders { get; set; }
        public int TotalProducts { get; set; }
        public int PendingOrders { get; set; }
        public decimal Revenue { get; set; }
    }
}
