using System.Collections.Generic;

namespace condominioApi.Models
{
    public class JunoEmbedded
    {        
        public JunoCharges _embedded {get; set;}
    }

    public class JunoCharges
    {
        public List<JunoCharge> charges {get; set;}
    }

}