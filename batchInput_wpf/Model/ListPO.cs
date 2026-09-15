namespace batchInput_wpf.Model
{
    public class ListPO
    {
        public int ID { get; set; }
        public string Po { get; set; } = string.Empty;
        public int IDGroup { get; set; }
        public bool isComplate { get; set; } = false;
        public string desPO { get; set; } = string.Empty;
        //public BatchInputHistory BatchInputHistory { get; set; } = new();
    }
}
