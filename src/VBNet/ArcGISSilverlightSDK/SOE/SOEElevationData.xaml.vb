﻿Imports Microsoft.VisualBasic
Imports System
Imports System.Collections.Generic
Imports System.Linq
Imports System.Net
Imports System.Windows
Imports System.Windows.Controls
Imports System.Windows.Input
Imports System.Windows.Media
Imports ESRI.ArcGIS.Client
Imports System.Json
Imports System.Windows.Media.Imaging
Imports ESRI.ArcGIS.Client.Symbols


Partial Public Class SOEElevationData
    Inherits UserControl
    Private myDrawObject As Draw
    Private webClient As WebClient
    Private colorRanges As New List(Of Color)()

    Public Sub New()
        InitializeComponent()
        myDrawObject = New Draw(MyMap) With {.DrawMode = DrawMode.Rectangle, .FillSymbol = TryCast(LayoutRoot.Resources("DrawFillSymbol"), ESRI.ArcGIS.Client.Symbols.FillSymbol), .IsEnabled = True}
        AddHandler myDrawObject.DrawComplete, AddressOf drawObject_DrawComplete

        webClient = New WebClient()
        AddHandler webClient.OpenReadCompleted, AddressOf wc_OpenReadCompleted

        colorRanges.Add(Colors.Blue)
        colorRanges.Add(Colors.Green)
        colorRanges.Add(Colors.Yellow)
        colorRanges.Add(Colors.Orange)
        colorRanges.Add(Colors.Red)
    End Sub

    Private Sub drawObject_DrawComplete(ByVal sender As Object, ByVal e As DrawEventArgs)
        myDrawObject.IsEnabled = False

        Dim aoiEnvelope As ESRI.ArcGIS.Client.Geometry.Envelope = TryCast(e.Geometry, ESRI.ArcGIS.Client.Geometry.Envelope)

        Dim SOEurl As String = "http://sampleserver4.arcgisonline.com/ArcGIS/rest/services/Elevation/ESRI_Elevation_World/MapServer/exts/ElevationsSOE/ElevationLayers/1/GetElevationData?"

        SOEurl &= String.Format("Extent={{""xmin"" : {0}, ""ymin"" : {1}, ""xmax"" : {2}, ""ymax"" :{3},""spatialReference"" : {{""wkid"" : {4}}}}}&Rows={5}&Columns={6}&f=json", aoiEnvelope.XMin, aoiEnvelope.YMin, aoiEnvelope.XMax, aoiEnvelope.YMax, MyMap.SpatialReference.WKID, HeightTextBox.Text, WidthTextBox.Text)

        webClient.OpenReadAsync(New Uri(SOEurl), aoiEnvelope)
    End Sub

    Private Sub wc_OpenReadCompleted(ByVal sender As Object, ByVal e As OpenReadCompletedEventArgs)
        Dim jsonObjectData As JsonObject = CType(JsonObject.Load(e.Result), JsonObject)
        e.Result.Close()

        If jsonObjectData.ContainsKey("error") Then
            MessageBox.Show(CInt(jsonObjectData("error")("code")).ToString() & ": " & jsonObjectData("error")("message").ToString())
            myDrawObject.IsEnabled = True
            Return
        End If

        Dim elevData As JsonArray = CType(jsonObjectData("data"), JsonArray)

        Dim thematicMin, thematicMax As Integer
        thematicMax = elevData(0)
        thematicMin = thematicMax

        For Each elevValue As Integer In elevData
            If elevValue < thematicMin Then
                thematicMin = elevValue
            End If
            If elevValue > thematicMax Then
                thematicMax = elevValue
            End If
        Next elevValue

        Dim totalRange As Integer = thematicMax - thematicMin
        Dim portion As Integer = totalRange \ 5

        Dim cellColor As New List(Of Color)()

        For Each elevValue As Integer In elevData
            Dim startValue As Integer = thematicMin
            For i As Integer = 0 To 4
                If Enumerable.Range(startValue, portion).Contains(elevValue) Then
                    cellColor.Add(colorRanges(i))
                    Exit For
                ElseIf i = 4 Then
                    cellColor.Add(colorRanges.Last())
                End If

                startValue = startValue + portion
            Next i
        Next elevValue

        Dim rows As Integer = Convert.ToInt32(HeightTextBox.Text)
        Dim cols As Integer = Convert.ToInt32(WidthTextBox.Text)
        Dim writeableBitmapElevation As New WriteableBitmap(rows, cols)

        Dim cell As Integer = 0

        For x As Integer = 0 To rows - 1
            For y As Integer = 0 To cols - 1
                SetPixel(writeableBitmapElevation, x, y, 255, cellColor(cell).R, cellColor(cell).G, cellColor(cell).B)
                cell += 1
            Next y
        Next x

        myDrawObject.IsEnabled = True

        Dim elementLayer As ESRI.ArcGIS.Client.ElementLayer = TryCast(MyMap.Layers("MyElementLayer"), ESRI.ArcGIS.Client.ElementLayer)
        Dim MyOutputImage As Image = TryCast(elementLayer.Children(0), Image)
        MyOutputImage.Source = writeableBitmapElevation
        MyOutputImage.SetValue(ESRI.ArcGIS.Client.ElementLayer.EnvelopeProperty, TryCast(e.UserState, ESRI.ArcGIS.Client.Geometry.Envelope))
    End Sub

    Private Sub SetPixel(ByVal bitmap As WriteableBitmap, ByVal row As Integer, ByVal col As Integer, ByVal alpha As Integer, ByVal red As Integer, ByVal green As Integer, ByVal blue As Integer)
        Dim index As Integer = row * bitmap.PixelWidth + col
        bitmap.Pixels(index) = alpha << 24 Or red << 16 Or green << 8 Or blue
    End Sub
End Class
