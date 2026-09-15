using batchInput_wpf.ViewsModel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace batchInput_wpf.Model
{
    public class BatchInputHistory:BaseViewModel
    {
        public int ID { get; set; }
        public string TerminalID { get; set; } = string.Empty;
        public string Machine { get; set; } = string.Empty;
        public bool start { get; set; } = false;
        public string Msnv { get; set; } = string.Empty;
        public string Shift { get; set; } = string.Empty;
        public bool isComplate { get; set; } = false;
        public DateTime? DateCreated { get; set; } = DateTime.Now;
        public int numRetry { get; set; } = 0;
        public string description { get; set; } = string.Empty;
        public List<ListPO> ListPO { get; set; } = new();

        //public int coutListPo { get; set; } = ListPO.Count;

        private ItemStatus _status = ItemStatus.Waiting;


        // Folder chứa ảnh/log lỗi
        public string ErrorPath { get; set; } = string.Empty;
        public ItemStatus Status
        {
            get => _status;
            set
            {
                _status = value;
                OnPropertyChanged();
            }
        }
    }
}
