using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FVN_REGISTER.Contract.Dtos.Approvals
{
    public class ApproverTreeNodeDto
    {
        public int DeptCode { get; set; }
        public string DeptName { get; set; } = "";
        public List<ApproverTreeLevelGroupDto> Levels { get; set; } = new();
    }
}
