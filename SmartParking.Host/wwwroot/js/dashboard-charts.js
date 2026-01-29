/* =========================================================
   SmartParking Dashboard Charts - Blazor Interop
   Chart.js integration for real-time dashboard data
========================================================= */

window.smartParkingCharts = (function () {
    let lineChart = null;
    let donutChart = null;

    function clamp(n, min, max) {
        return Math.max(min, Math.min(max, n));
    }

    function rand(min, max) {
        return Math.floor(Math.random() * (max - min + 1)) + min;
    }

    // Color palette for multiple slots (distinct colors for each line)
    const slotColors = [
        { bg: 'rgba(79, 195, 255, 0.15)', border: 'rgba(79, 195, 255, 1)' },
        { bg: 'rgba(255, 99, 132, 0.15)', border: 'rgba(255, 99, 132, 1)' },
        { bg: 'rgba(255, 206, 86, 0.15)', border: 'rgba(255, 206, 86, 1)' },
        { bg: 'rgba(75, 192, 192, 0.15)', border: 'rgba(75, 192, 192, 1)' },
        { bg: 'rgba(153, 102, 255, 0.15)', border: 'rgba(153, 102, 255, 1)' },
        { bg: 'rgba(255, 159, 64, 0.15)', border: 'rgba(255, 159, 64, 1)' },
        { bg: 'rgba(54, 162, 235, 0.15)', border: 'rgba(54, 162, 235, 1)' },
        { bg: 'rgba(231, 233, 237, 0.15)', border: 'rgba(231, 233, 237, 1)' },
        { bg: 'rgba(46, 204, 113, 0.15)', border: 'rgba(46, 204, 113, 1)' },
        { bg: 'rgba(231, 76, 60, 0.15)', border: 'rgba(231, 76, 60, 1)' }
    ];

    function initCharts(data, slotHourlyData) {
        const { free, occupied, offline, total } = data;

        // X-axis labels: hours of the day (00:00 - 23:00)
        const hourLabels = Array.from({ length: 24 }, (_, i) => `${String(i).padStart(2, '0')}:00`);

        // Initialize multi-line chart for slot occupancy
        const lineCtx = document.getElementById('chartLine');
        if (lineCtx) {
            if (lineChart) {
                lineChart.destroy();
            }
            
            // Create a dataset for each slot
            const datasets = [];
            
            if (slotHourlyData && slotHourlyData.length > 0) {
                slotHourlyData.forEach((slot, index) => {
                    const colorIndex = index % slotColors.length;
                    datasets.push({
                        label: slot.slotLabel,
                        data: slot.hourlyMinutes || new Array(24).fill(0),
                        tension: 0.3,
                        borderWidth: 2,
                        pointRadius: 3,
                        pointHoverRadius: 5,
                        fill: false,
                        backgroundColor: slotColors[colorIndex].bg,
                        borderColor: slotColors[colorIndex].border,
                        pointBackgroundColor: slotColors[colorIndex].border
                    });
                });
            } else {
                // No data - show empty chart
                datasets.push({
                    label: 'No data',
                    data: new Array(24).fill(0),
                    tension: 0.3,
                    borderWidth: 2,
                    pointRadius: 0,
                    fill: false,
                    backgroundColor: 'rgba(107, 114, 128, 0.15)',
                    borderColor: 'rgba(107, 114, 128, 0.5)'
                });
            }

            lineChart = new Chart(lineCtx, {
                type: 'line',
                data: {
                    labels: hourLabels,
                    datasets: datasets
                },
                options: {
                    responsive: true,
                    maintainAspectRatio: false,
                    interaction: {
                        mode: 'index',
                        intersect: false
                    },
                    plugins: {
                        legend: { 
                            display: true,
                            position: 'top',
                            labels: {
                                color: 'rgba(234, 242, 255, 0.85)',
                                usePointStyle: true,
                                pointStyle: 'circle',
                                padding: 15,
                                font: { size: 11, weight: 'bold' }
                            }
                        },
                        tooltip: { 
                            callbacks: {
                                label: function(context) {
                                    const minutes = context.raw;
                                    const slotLabel = context.dataset.label;
                                    if (minutes >= 60) {
                                        const hours = Math.floor(minutes / 60);
                                        const mins = Math.round(minutes % 60);
                                        return `${slotLabel}: ${hours}h ${mins}m occupied`;
                                    }
                                    return `${slotLabel}: ${Math.round(minutes)} min occupied`;
                                }
                            }
                        }
                    },
                    scales: {
                        x: {
                            grid: { color: 'rgba(255, 255, 255, 0.06)' },
                            ticks: { 
                                color: 'rgba(234, 242, 255, 0.70)',
                                font: { size: 10 },
                                maxRotation: 45,
                                minRotation: 0
                            },
                            title: {
                                display: true,
                                text: 'Hour of Day',
                                color: 'rgba(234, 242, 255, 0.70)'
                            }
                        },
                        y: {
                            grid: { color: 'rgba(255, 255, 255, 0.06)' },
                            ticks: { 
                                color: 'rgba(234, 242, 255, 0.70)',
                                callback: function(value) {
                                    return Math.round(value) + 'm';
                                }
                            },
                            beginAtZero: true,
                            max: 60, // Max 60 minutes per hour
                            title: {
                                display: true,
                                text: 'Minutes Occupied',
                                color: 'rgba(234, 242, 255, 0.70)'
                            }
                        }
                    }
                }
            });
        }

        // Initialize donut chart
        const donutCtx = document.getElementById('chartDonut');
        if (donutCtx) {
            if (donutChart) {
                donutChart.destroy();
            }
            
            donutChart = new Chart(donutCtx, {
                type: 'doughnut',
                data: {
                    labels: ['Free', 'Occupied', 'Offline'],
                    datasets: [{
                        data: [free, occupied, offline],
                        backgroundColor: [
                            'rgba(51, 214, 159, 0.85)',
                            'rgba(255, 77, 94, 0.85)',
                            'rgba(107, 114, 128, 0.80)'
                        ],
                        borderWidth: 0
                    }]
                },
                options: {
                    responsive: true,
                    maintainAspectRatio: false,
                    cutout: '68%',
                    plugins: {
                        legend: { display: false }
                    }
                }
            });
        }

        // Calculate most occupied slot (sum of all hourly minutes)
        let peakSlot = 'N/A';
        let maxMinutes = 0;
        if (slotHourlyData && slotHourlyData.length > 0) {
            for (const slot of slotHourlyData) {
                const totalMinutes = (slot.hourlyMinutes || []).reduce((sum, m) => sum + m, 0);
                if (totalMinutes > maxMinutes) {
                    maxMinutes = totalMinutes;
                    peakSlot = slot.slotLabel;
                }
            }
            if (maxMinutes > 0) {
                if (maxMinutes >= 60) {
                    const hours = Math.floor(maxMinutes / 60);
                    const mins = Math.round(maxMinutes % 60);
                    peakSlot = `${peakSlot} (${hours}h ${mins}m)`;
                } else {
                    peakSlot = `${peakSlot} (${Math.round(maxMinutes)} min)`;
                }
            }
        }

        return {
            peakHour: peakSlot,
            avgOccupancy: 0
        };
    }

    function updateDonut(data) {
        const { free, occupied, offline } = data;
        
        if (donutChart) {
            donutChart.data.datasets[0].data = [free, occupied, offline];
            donutChart.update();
        }
    }

    function destroyCharts() {
        if (lineChart) {
            lineChart.destroy();
            lineChart = null;
        }
        if (donutChart) {
            donutChart.destroy();
            donutChart = null;
        }
    }

    return {
        initCharts,
        updateDonut,
        destroyCharts
    };
})();
