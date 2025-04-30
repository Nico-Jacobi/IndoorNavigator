package com.example.locationrequestunityplugin

import android.app.Activity
import android.content.IntentSender
import android.util.Log
import com.google.android.gms.common.api.ResolvableApiException
import com.google.android.gms.location.*

object LocationPrompt {
    @JvmStatic
    fun promptForLocation(activity: Activity) {
        val locationRequest = LocationRequest.create().apply {
            priority = LocationRequest.PRIORITY_HIGH_ACCURACY
            interval = 10000
            fastestInterval = 5000
        }

        val builder = LocationSettingsRequest.Builder()
            .addLocationRequest(locationRequest)
            .setAlwaysShow(true)

        val client: SettingsClient = LocationServices.getSettingsClient(activity)

        client.checkLocationSettings(builder.build())
            .addOnSuccessListener {
                Log.d("LocationPrompt", "Location settings are fine.")
            }
            .addOnFailureListener { e ->
                if (e is ResolvableApiException) {
                    try {
                        e.startResolutionForResult(activity, 1234)
                    } catch (sendEx: IntentSender.SendIntentException) {
                        Log.e("LocationPrompt", "Failed to show dialog", sendEx)
                    }
                }
            }
    }
}
