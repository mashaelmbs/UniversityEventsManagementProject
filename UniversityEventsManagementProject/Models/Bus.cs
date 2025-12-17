using System;
using System.Collections.Generic;

namespace UniversityEventsManagement.Models
{
    public class Bus
    {
        public int BusID { get; set; }
        public int EventID { get; set; }
        public string? BusNumber { get; set; }
        public int Capacity { get; set; }
        public DateTime DepartureTime { get; set; }
        public string? DepartureLocation { get; set; }
        public string? DestinationLocation { get; set; }
        public int CurrentPassengers { get; set; }

        // Navigation properties
        public virtual Event? Event { get; set; }
        public virtual ICollection<BusReservation>? Reservations { get; set; }
    }
}
