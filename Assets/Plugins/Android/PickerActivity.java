package com.lostpetnetwork.gallery;

import android.app.Activity;
import android.content.Intent;
import android.graphics.Bitmap;
import android.graphics.BitmapFactory;
import android.graphics.Matrix;
import android.media.ExifInterface;
import android.net.Uri;
import android.os.Bundle;

import com.unity3d.player.UnityPlayer;

import java.io.File;
import java.io.FileOutputStream;
import java.io.InputStream;

/**
 * Opens the phone's own photo chooser, turns the chosen picture into an upright, reduced JPEG in the app's cache
 * folder and tells Unity where it is. An empty path means the user cancelled. No storage permission is needed:
 * the system chooser hands over only the one picture the user picks.
 */
public class PickerActivity extends Activity {
    private static final int REQUEST = 7301;
    private static final int MAX_SIDE = 1600;
    private static final String CALLBACK_OBJECT = "GalleryPickerBridge";
    private static final String CALLBACK_METHOD = "OnPhotoPicked";

    @Override
    protected void onCreate(Bundle state) {
        super.onCreate(state);
        if (state != null) return; // recreated while the chooser is open: the result is still on its way
        Intent intent = new Intent(Intent.ACTION_GET_CONTENT);
        intent.setType("image/*");
        intent.addCategory(Intent.CATEGORY_OPENABLE);
        startActivityForResult(Intent.createChooser(intent, "Choose a photo"), REQUEST);
    }

    @Override
    protected void onActivityResult(int requestCode, int resultCode, Intent data) {
        super.onActivityResult(requestCode, resultCode, data);
        String path = "";
        if (requestCode == REQUEST && resultCode == RESULT_OK && data != null && data.getData() != null) {
            try {
                path = copyUpright(data.getData());
            } catch (Throwable ignored) {
                path = "";
            }
        }
        UnityPlayer.UnitySendMessage(CALLBACK_OBJECT, CALLBACK_METHOD, path);
        finish();
    }

    private String copyUpright(Uri uri) throws Exception {
        int rotation = 0;
        try (InputStream in = getContentResolver().openInputStream(uri)) {
            ExifInterface exif = new ExifInterface(in);
            int orientation = exif.getAttributeInt(ExifInterface.TAG_ORIENTATION, ExifInterface.ORIENTATION_NORMAL);
            if (orientation == ExifInterface.ORIENTATION_ROTATE_90) rotation = 90;
            else if (orientation == ExifInterface.ORIENTATION_ROTATE_180) rotation = 180;
            else if (orientation == ExifInterface.ORIENTATION_ROTATE_270) rotation = 270;
        } catch (Throwable ignored) {
            // no orientation information: keep the picture as it is
        }

        BitmapFactory.Options bounds = new BitmapFactory.Options();
        bounds.inJustDecodeBounds = true;
        try (InputStream in = getContentResolver().openInputStream(uri)) {
            BitmapFactory.decodeStream(in, null, bounds);
        }
        int sample = 1;
        while (Math.max(bounds.outWidth, bounds.outHeight) / sample > MAX_SIDE * 2) sample *= 2;

        BitmapFactory.Options options = new BitmapFactory.Options();
        options.inSampleSize = sample;
        Bitmap bitmap;
        try (InputStream in = getContentResolver().openInputStream(uri)) {
            bitmap = BitmapFactory.decodeStream(in, null, options);
        }
        if (bitmap == null) return "";

        float scale = Math.min(1f, (float) MAX_SIDE / Math.max(bitmap.getWidth(), bitmap.getHeight()));
        Matrix matrix = new Matrix();
        if (scale < 1f) matrix.postScale(scale, scale);
        if (rotation != 0) matrix.postRotate(rotation);
        Bitmap result = Bitmap.createBitmap(bitmap, 0, 0, bitmap.getWidth(), bitmap.getHeight(), matrix, true);

        File file = new File(getCacheDir(), "picked_photo.jpg");
        try (FileOutputStream out = new FileOutputStream(file)) {
            result.compress(Bitmap.CompressFormat.JPEG, 88, out);
        }
        return file.getAbsolutePath();
    }
}
