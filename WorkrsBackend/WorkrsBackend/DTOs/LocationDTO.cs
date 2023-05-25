namespace WorkrsBackend.DTOs
{
    public class LocationDTO
    {
        public string Name {  get; set; }
        public decimal Latitude { get; set; }
        public decimal Longitude { get; set; }

        public LocationDTO(string name, decimal latitude, decimal longitude)
        {
            Name = name;
            Latitude = latitude;
            Longitude = longitude;
        }
    }
}
