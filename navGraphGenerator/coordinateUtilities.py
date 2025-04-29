from typing import Tuple
import math

# used to normalize the coordinates for unity
origin_lat=50.80977
origin_lon=8.81048

def meters_to_latlon(x_meter: float, y_meter: float, origin_lat: float, origin_lon: float) -> Tuple[float, float]:
    """
    Converts coordinates (x, y) in meters to latitude and longitude.
    Assumes flat Earth approximation (valid for small distances).

    :param x_meter: Easting in meters (corresponds to longitude)
    :param y_meter: Northing in meters (corresponds to latitude)
    :param origin_lat: Latitude of the origin point
    :param origin_lon: Longitude of the origin point
    :return: (latitude, longitude)
    """
    meters_per_degree_lat = 111320 # fixed as earth is a sphere
    meters_per_degree_lon = 40075000 * math.cos(math.radians(origin_lat)) / 360.0

    delta_lat = y_meter / meters_per_degree_lat
    delta_lon = x_meter / meters_per_degree_lon


    return delta_lat, delta_lon


def latlon_to_meters(lat: float, lon: float, origin_lat: float, origin_lon: float) -> Tuple[float, float]:
    """
    Converts latitude and longitude to meters from origin point.
    Assumes flat Earth approximation (valid for small distances).

    :param lat: Target latitude
    :param lon: Target longitude
    :param origin_lat: Latitude of the origin point
    :param origin_lon: Longitude of the origin point
    :return: (x_meter, y_meter) where x is easting and y is northing
    """
    meters_per_degree_lat = 111320 # fixed as earth is a sphere
    meters_per_degree_lon = 40075000 * math.cos(math.radians(origin_lat)) / 360.0

    y_meter = lat * meters_per_degree_lat
    x_meter = lon * meters_per_degree_lon

    return x_meter, y_meter


def normalize_lat_lon_to_meter(lat: float, lon: float, origin_lat: float, origin_lon: float) -> Tuple[float, float]:
    """
    Normalizes a GPS coordinate pair by first converting to meters from the origin point,
    then scaling based on the specified width and height in meters.

    :param lat: Latitude coordinate to normalize
    :param lon: Longitude coordinate to normalize
    :param origin_lat: Latitude of the origin point (reference point)
    :param origin_lon: Longitude of the origin point (reference point)
    :return: A tuple containing normalized x and y values in meters (0-100 range)
    """


    # Convert lat/lon to meters from origin
    x_meter, y_meter = latlon_to_meters(lat-origin_lat, lon-origin_lon, origin_lat, origin_lon)

    return x_meter, -y_meter
